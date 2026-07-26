"""
build_index.py — Build / refresh the ChromaDB index.

Sources:
  CSV file (default — legacy behavior, unchanged):
      python build_index.py                                  # defaults to devprep_rag_knowledge.csv
      python build_index.py path/to/devprep_rag_knowledge.csv
  QuestionAPI (SQL Server question bank via the internal export endpoint):
      python build_index.py --source api
      # Reads QUESTION_API_URL (default http://localhost:7004) and INTERNAL_API_KEY from env;
      # the repo-root .env is loaded automatically (via app.core.config).

Seeding is an OFFLINE, manual step. Run it from the ai-service venv on the HOST — NOT inside
the Docker container — so localhost:7004 reaches QuestionAPI directly (inside the container,
localhost would not resolve to the host). QuestionAPI must be running for --source api.

After seeding ./chroma_data (venv mode), copy it to ./chroma_data_docker so the container
picks up the new index (same manual step as the old CSV flow; a plain `docker compose restart`
does NOT re-seed anything).
"""
import argparse
import os

from app.core import config  # noqa: F401  (side effect: loads the repo-root .env)
from app.services.chroma_store import CHROMA_DIR, COLLECTION, index_csv, index_from_api


def main() -> None:
    parser = argparse.ArgumentParser(description="Build / refresh the ChromaDB question index.")
    parser.add_argument(
        "csv_path", nargs="?", default="devprep_rag_knowledge.csv",
        help="CSV file to index (csv mode only).")
    parser.add_argument(
        "--source", choices=["csv", "api"], default="csv",
        help="Question source: 'csv' (legacy file, default) or 'api' (QuestionAPI export).")
    args = parser.parse_args()

    if args.source == "api":
        base_url = os.getenv("QUESTION_API_URL", "http://localhost:7004")
        api_key = os.getenv("INTERNAL_API_KEY", "")
        count = index_from_api(base_url, api_key)
        print(f"Indexed {count} questions from QuestionAPI ({base_url}) "
              f"into collection '{COLLECTION}' at {CHROMA_DIR}")
    else:
        count = index_csv(args.csv_path)
        print(f"Indexed {count} questions from {args.csv_path!r} "
              f"into collection '{COLLECTION}' at {CHROMA_DIR}")


if __name__ == "__main__":
    main()
