"""
prepare_dataset.py — Tải dữ liệu CV/JD THẬT ngành IT từ HuggingFace (Djinni, license MIT)
và xuất ra raw_texts.jsonl cho bước gán nhãn + train (train_kaggle.py).

Nguồn (dữ liệu thật từ nền tảng tuyển dụng IT Djinni, 2020-2023, đã ẩn danh):
  - lang-uk/recruitment-dataset-candidate-profiles-english  (~210k CV IT)
  - lang-uk/recruitment-dataset-job-descriptions-english    (~150k JD IT)

Chạy (Kaggle/Colab/local có internet):
    pip install datasets
    python prepare_dataset.py --cv 1400 --jd 1100 --out raw_texts.jsonl

Output: mỗi dòng JSON {"id": "...", "type": "cv"|"jd", "text": "..."}
"""
from __future__ import annotations

import argparse
import hashlib
import json
import random

from datasets import load_dataset

CV_DATASET = "lang-uk/recruitment-dataset-candidate-profiles-english"
JD_DATASET = "lang-uk/recruitment-dataset-job-descriptions-english"

CV_TEXT_COLUMNS = ["CV", "cv", "Moreinfo", "moreinfo", "text"]
JD_TEXT_COLUMNS = ["Long Description", "long_description", "Description", "description", "text"]

MIN_CHARS, MAX_CHARS = 400, 6000


def pick_text_column(ds, preferred: list[str]) -> str:
    for c in preferred:
        if c in ds.column_names:
            return c
    sample = ds.select(range(min(200, len(ds))))
    best, best_len = None, -1
    for c in ds.column_names:
        vals = [v for v in sample[c] if isinstance(v, str)]
        if not vals:
            continue
        avg = sum(len(v) for v in vals) / len(vals)
        if avg > best_len:
            best, best_len = c, avg
    if best is None:
        raise RuntimeError(f"No text column found; columns = {ds.column_names}")
    return best


def compose_cv_text(row: dict, body_col: str) -> str:
    parts = []
    if row.get("Position"):
        parts.append(str(row["Position"]))
    exp = row.get("Experience Years") or row.get("experience_years")
    if exp not in (None, ""):
        parts.append(f"Experience: {exp} years")
    if row.get("Highlights"):
        parts.append(str(row["Highlights"]))
    body = row.get(body_col) or ""
    parts.append(str(body))
    return "\n\n".join(p.strip() for p in parts if p and str(p).strip())


def compose_jd_text(row: dict, body_col: str) -> str:
    parts = []
    if row.get("Position"):
        parts.append(f"Position: {row['Position']}")
    exp = row.get("Experience Years") or row.get("experience_years")
    if exp not in (None, ""):
        parts.append(f"Experience required: {exp} years")
    parts.append(str(row.get(body_col) or ""))
    return "\n\n".join(p.strip() for p in parts if p and str(p).strip())


def collect(dataset_id: str, kind: str, preferred_cols: list[str], want: int, rng: random.Random):
    print(f"Loading {dataset_id} ...")
    ds = load_dataset(dataset_id, split="train")
    body_col = pick_text_column(ds, preferred_cols)
    print(f"  rows={len(ds)}, body column = '{body_col}'")

    idx = list(range(len(ds)))
    rng.shuffle(idx)

    seen: set[str] = set()
    out = []
    for i in idx:
        row = ds[i]
        text = compose_cv_text(row, body_col) if kind == "cv" else compose_jd_text(row, body_col)
        if not (MIN_CHARS <= len(text) <= MAX_CHARS):
            continue
        h = hashlib.md5(text[:300].lower().encode()).hexdigest()
        if h in seen:
            continue
        seen.add(h)
        out.append({"id": f"{kind}-{h[:12]}", "type": kind, "text": text})
        if len(out) >= want:
            break
    print(f"  kept {len(out)} {kind} samples")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cv", type=int, default=1400, help="số mẫu CV")
    ap.add_argument("--jd", type=int, default=1100, help="số mẫu JD")
    ap.add_argument("--out", default="raw_texts.jsonl")
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    samples = collect(CV_DATASET, "cv", CV_TEXT_COLUMNS, args.cv, rng)
    samples += collect(JD_DATASET, "jd", JD_TEXT_COLUMNS, args.jd, rng)
    rng.shuffle(samples)

    with open(args.out, "w", encoding="utf-8") as f:
        for s in samples:
            f.write(json.dumps(s, ensure_ascii=False) + "\n")
    print(f"\nWrote {len(samples)} samples -> {args.out}")


if __name__ == "__main__":
    main()
