"""
build_index.py — Build / refresh the ChromaDB index from the knowledge CSV.

Run from the ai-service directory (so the `app` package is importable):
    python build_index.py                                  # defaults to devprep_rag_knowledge.csv
    python build_index.py path/to/devprep_rag_knowledge.csv
"""
import sys

from app.services.chroma_store import CHROMA_DIR, COLLECTION, index_csv


def main() -> None:
    path = sys.argv[1] if len(sys.argv) > 1 else "devprep_rag_knowledge.csv"
    count = index_csv(path)
    print(f"Indexed {count} questions from {path!r} into collection '{COLLECTION}' at {CHROMA_DIR}")


if __name__ == "__main__":
    main()