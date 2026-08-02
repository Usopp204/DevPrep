"""
chroma_store.py — Index & query the DevPrep RAG knowledge base.
Location: ai-service/app/services/chroma_store.py

Dependencies (add to requirements.txt): chromadb, sentence-transformers

Usage:
    python -m app.services.chroma_store devprep_rag_knowledge.csv   # build index
"""
from __future__ import annotations

import csv
import hashlib
import os
import random
import re
from pathlib import Path

import chromadb
from chromadb.utils import embedding_functions

CHROMA_DIR = os.getenv("CHROMA_DIR", "./chroma_data")
COLLECTION = "devprep_qa"
# Multilingual, no "query:/passage:" prefix needed (unlike e5).
# Limited GPU? switch to "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2" (~470MB).
EMBED_MODEL = os.getenv("EMBED_MODEL", "BAAI/bge-m3")

# Two questions whose embeddings are at least this cosine-similar are treated as
# near-duplicates within a single interview (catches paraphrases like
# "advantages of X" vs "benefit of X"). Tune 0.85-0.95 if needed.
NEAR_DUP_THRESHOLD = float(os.getenv("NEAR_DUP_THRESHOLD", "0.92"))

_client = chromadb.PersistentClient(path=CHROMA_DIR)
_embed = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=EMBED_MODEL)
_collection = _client.get_or_create_collection(name=COLLECTION, embedding_function=_embed)
_catalog_cache: dict[str, list[str]] | None = None


# --- Clean the Answer field: scraped data is polluted with HTML/markdown junk -
_IMG = re.compile(r"<img[^>]*>")
_BACK_TO_TOP = re.compile(r"<div[^>]*>\s*<b>.*?back to top.*?</b>\s*</div>", re.I | re.S)
_TAG = re.compile(r"<[^>]+>")
_MULTISPACE = re.compile(r"[ \t]{2,}")


def _clean(text: str) -> str:
    if not text:
        return ""
    text = text.replace("\\'", "'").replace('\\"', '"')  # unescape \' \"
    text = _IMG.sub("", text)            # drop images pointing to non-existent files
    text = _BACK_TO_TOP.sub("", text)    # drop "back to top" navigation links
    text = _TAG.sub("", text)            # strip remaining HTML tags, keep text + markdown tables
    text = _MULTISPACE.sub(" ", text)
    return text.strip()


def _uid(question: str, position: str) -> str:
    """Deterministic id for idempotent upsert (re-running won't duplicate records)."""
    return hashlib.sha1(f"{position}|{question}".encode()).hexdigest()


def _collect_records(rows) -> dict[str, tuple[str, dict]]:
    """Normalize raw rows into {uid: (question_text, metadata)} — the single shared code path
    for every question source (CSV file, QuestionAPI export), so document text / metadata /
    ID / dedup can never drift between sources.

    Rows use the CSV header key casing: Question, Answer, Category, Difficulty, Position.
    Document ID: the row's explicit "Id" (SQL QuestionId GUID — stable across content edits,
    required for the incremental upsert/delete flow) when present; otherwise the legacy
    SHA1(position|question) content hash (CSV path — kept unchanged as fallback, where
    duplicate ids also collapse exact Position+Question duplicates, last occurrence wins).
    """
    records: dict[str, tuple[str, dict]] = {}
    for row in rows:
        question = (row["Question"] or "").strip()
        if not question:
            continue
        uid = (row.get("Id") or "").strip() or _uid(question, row["Position"])
        records[uid] = (question, {
            "answer": _clean(row["Answer"]),
            "category": (row["Category"] or "").strip(),
            "difficulty": (row["Difficulty"] or "").strip().lower(),  # 'Junior' -> 'junior'
            "position": (row["Position"] or "").strip(),
        })
    return records


def _upsert_records(records: dict[str, tuple[str, dict]], batch_size: int) -> int:
    """Upsert normalized records in batches to stay within ChromaDB limits."""
    items = list(records.items())
    for i in range(0, len(items), batch_size):
        chunk = items[i:i + batch_size]
        _collection.upsert(
            ids=[uid for uid, _ in chunk],
            documents=[doc for _, (doc, _) in chunk],
            metadatas=[meta for _, (_, meta) in chunk],
        )
    _invalidate_catalog_cache()
    return len(records)


def index_csv(csv_path: str | Path, batch_size: int = 1000) -> int:
    """Load a CSV (Question, Answer, Category, Difficulty, Position) into ChromaDB."""
    with open(csv_path, newline="", encoding="utf-8") as f:
        records = _collect_records(csv.DictReader(f))
    return _upsert_records(records, batch_size)


def _reset_collection() -> None:
    """Clear every existing document so the re-seed starts from an empty index. Needed when
    re-seeding from the API: document IDs are SQL GUIDs there, so upserting over an old index
    (SHA1 content-hash IDs) would ADD documents next to the stale ones — the collection would
    double. Deletes by id (batched) instead of dropping the collection: dropping invalidates
    the handle a running service holds AND the chromadb client can hand back a stale cached
    handle for the recreated collection (upserts then fail with 'Collection does not exist')."""
    total = _collection.count()
    if not total:
        return
    print(f"[chroma_store] WARNING: clearing {total} existing documents from collection "
          f"'{COLLECTION}' at {CHROMA_DIR} (full re-seed, ID scheme = SQL QuestionId).")
    while True:
        ids = _collection.get(limit=5000)["ids"]
        if not ids:
            break
        _collection.delete(ids=ids)
    _invalidate_catalog_cache()


def reset_index() -> None:
    """Remove all indexed questions before a full SQL Server reconciliation."""
    _reset_collection()


def index_from_api(base_url: str, api_key: str, batch_size: int = 1000) -> int:
    """Load the question bank from QuestionAPI's internal export endpoint into ChromaDB.

    Replaces the CSV as the question source: GET {base_url}/internal/questions/export
    (auth: X-Internal-Api-Key). The endpoint returns the SQL question bank as a JSON array of
    {questionId, question, answer, position, category, difficulty} with difficulty in DB casing
    ("Senior"/"Middle"); _collect_records lowercases it exactly like the CSV path did, which
    build_question_pool's filters depend on.

    Document ID = questionId (SQL GUID), matching the incremental upsert/delete endpoints, so
    admin edits update the same document instead of leaving stale content-hash twins. Because of
    that, this ALWAYS drops and recreates the collection first (see _reset_collection).

    Run OFFLINE from the ai-service venv on the HOST (not inside Docker) so localhost:7004
    reaches QuestionAPI — see build_index.py for the full seeding procedure.
    """
    import requests

    url = f"{base_url.rstrip('/')}/internal/questions/export"
    resp = requests.get(url, headers={"X-Internal-Api-Key": api_key}, timeout=300)
    resp.raise_for_status()

    # Map the API's JSON keys onto the CSV header casing so both sources share _collect_records.
    rows = (
        {
            "Id": r.get("questionId"),
            "Question": r.get("question"),
            "Answer": r.get("answer"),
            "Category": r.get("category"),
            "Difficulty": r.get("difficulty"),
            "Position": r.get("position"),
        }
        for r in resp.json()
    )
    records = _collect_records(rows)
    _reset_collection()
    return _upsert_records(records, batch_size)


def upsert_question(question_id: str, question: str, answer: str | None = None,
                    category: str | None = None, difficulty: str | None = None,
                    position: str | None = None) -> None:
    """Incremental single-document upsert (called by QuestionAPI on admin create/update/restore).
    Runs through _collect_records so normalization (difficulty lowercase, answer _clean) can
    never drift from the batch seed path."""
    records = _collect_records([{
        "Id": question_id,
        "Question": question,
        "Answer": answer,
        "Category": category,
        "Difficulty": difficulty,
        "Position": position,
    }])
    _upsert_records(records, batch_size=1)


def delete_question(question_id: str) -> None:
    """Remove one document by ID (called by QuestionAPI on admin soft-delete). Deleting an id
    that is not in the collection is a no-op."""
    _collection.delete(ids=[question_id])
    _invalidate_catalog_cache()


def _drop_near_duplicates(pool: list[dict], embeddings) -> list[dict]:
    """Greedily drop questions whose embedding is near-identical to one already kept."""
    if embeddings is None or len(embeddings) != len(pool):
        return pool
    import numpy as np

    kept: list[dict] = []
    kept_mat = None
    for item, emb in zip(pool, embeddings):
        v = np.asarray(emb, dtype=np.float32)
        norm = float(np.linalg.norm(v))
        if norm == 0.0:
            kept.append(item)
            continue
        v = v / norm
        if kept_mat is not None and float((kept_mat @ v).max()) >= NEAR_DUP_THRESHOLD:
            continue  # too similar to a question already in the pool
        kept.append(item)
        kept_mat = v[None, :] if kept_mat is None else np.vstack([kept_mat, v])
    return kept


def build_question_pool(
    position: str | None = None,
    difficulty: str | None = None,
    categories: list[str] | None = None,
    limit: int | None = None,
) -> list[dict]:
    """
    Fetch the question pool for an exact-match level via metadata filter — NO semantic search.
    Any of position / difficulty / categories may be omitted. When `categories` (technologies)
    are given without a position, questions are matched by technology across all tracks, so a
    skill like MongoDB (stored under the Database track) is reachable for a Backend interview.
    """
    conds: list[dict] = []
    if position:
        conds.append({"position": position})
    if difficulty:
        conds.append({"difficulty": difficulty.lower()})
    if categories:
        conds.append({"category": {"$in": list(categories)}})
    if not conds:
        return []
    where = conds[0] if len(conds) == 1 else {"$and": conds}

    res = _collection.get(where=where, include=["documents", "metadatas", "embeddings"])
    pool = [
        {"qid": qid, "question": doc, **meta}
        for qid, doc, meta in zip(res["ids"], res["documents"], res["metadatas"])
    ]
    pool = _drop_near_duplicates(pool, res.get("embeddings"))
    if limit and len(pool) > limit:
        random.shuffle(pool)
        pool = pool[:limit]
    return pool


def _invalidate_catalog_cache() -> None:
    global _catalog_cache
    _catalog_cache = None


def catalog() -> dict[str, list[str]]:
    """Return {position: sorted distinct categories} from the indexed data (cached).

    Powers the dashboard so it only offers roles/technologies that actually have questions.
    Scans metadata in batches: a single get() over a large collection overflows SQLite's
    variable limit ("too many SQL variables").
    """
    global _catalog_cache
    if _catalog_cache is None:
        by_pos: dict[str, set] = {}
        batch = 2000
        offset = 0
        while True:
            res = _collection.get(include=["metadatas"], limit=batch, offset=offset)
            metas = res["metadatas"]
            if not metas:
                break
            for meta in metas:
                pos = (meta.get("position") or "").strip()
                cat = (meta.get("category") or "").strip()
                if pos and cat:
                    by_pos.setdefault(pos, set()).add(cat)
            if len(metas) < batch:
                break
            offset += batch
        _catalog_cache = {p: sorted(c) for p, c in by_pos.items()}
    return _catalog_cache


def query_similar(text: str, category: str | None = None, k: int = 2) -> list[dict]:
    """Find related Q&A (semantic) to enrich context for the Evaluator (design section 4.4)."""
    res = _collection.query(
        query_texts=[text],
        n_results=k,
        where={"category": category} if category else None,
        include=["documents", "metadatas"],
    )
    return [
        {"question": doc, **meta}
        for doc, meta in zip(res["documents"][0], res["metadatas"][0])
    ]


if __name__ == "__main__":
    import sys

    path = sys.argv[1] if len(sys.argv) > 1 else "devprep_rag_knowledge.csv"
    n = index_csv(path)
    print(f"Indexed {n} questions into '{COLLECTION}' at {CHROMA_DIR}")
