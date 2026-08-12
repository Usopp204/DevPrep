#!/usr/bin/env python3
"""
clean-ssms-export.py — strip database-level statements from SSMS "Generate Scripts"
output and write the result into the db/baseline/ placeholders.

Run from the repository root:
    python db/tools/clean-ssms-export.py SQL

Why this exists: SSMS emits a CREATE DATABASE block with absolute Windows FILENAME
paths and ~35 ALTER DATABASE SET statements that cannot run inside a Linux container.
The schema objects themselves are fine. See db/README.md section 2 for the SSMS
options that prevent most of this being emitted in the first place.

DESIGN NOTE — why this is a statement-aware walker, not a line filter.
The last named constraint in a file is typically followed IMMEDIATELY by the tail
block:

    ALTER TABLE [dbo].[UserProfiles] ADD CONSTRAINT [DF_UserProfiles_UpdatedAt] ...
    GO
    USE [master]
    GO
    ALTER DATABASE [DevPrep_User] SET READ_WRITE
    GO

A filter that cuts "from the last USE [master] onward", or that matches ALTER.*DATABASE
loosely, silently eats that constraint. Matching is therefore anchored with
startswith() on the stripped, upper-cased line, so ALTER DATABASE and ALTER TABLE can
never be confused. Every line removed after the cut point is reported, so nothing
disappears without appearing in the output.
"""
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BASELINE_DIR = REPO / "db" / "baseline"

MARKER_BEGIN = "PASTE SSMS OUTPUT BELOW THIS LINE"
MARKER_END = "END OF PASTED CONTENT"
FIRST_OBJECT = "/****** Object:  Table"

# Statement types counted for the report. Anything matching none of these but starting
# with CREATE/ALTER is surfaced as "unexpected" rather than silently passed through.
COUNTERS = {
    "CREATE TABLE": re.compile(r"^\s*CREATE\s+TABLE\s", re.I),
    "CREATE INDEX": re.compile(r"^\s*CREATE\s+(UNIQUE\s+)?(CLUSTERED\s+|NONCLUSTERED\s+)?INDEX\s", re.I),
    "ALTER TABLE ADD CONSTRAINT": re.compile(r"^\s*ALTER\s+TABLE\s+.*\sADD\s+CONSTRAINT\s", re.I),
    "ALTER TABLE ADD DEFAULT (unnamed)": re.compile(r"^\s*ALTER\s+TABLE\s+.*\sADD\s+DEFAULT\s", re.I),
    "ALTER TABLE CHECK CONSTRAINT": re.compile(r"^\s*ALTER\s+TABLE\s+.*\sCHECK\s+CONSTRAINT\s", re.I),
    "CREATE VIEW": re.compile(r"^\s*CREATE\s+VIEW\s", re.I),
    "CREATE PROCEDURE": re.compile(r"^\s*CREATE\s+(OR\s+ALTER\s+)?PROC(EDURE)?\s", re.I),
    "CREATE FUNCTION": re.compile(r"^\s*CREATE\s+(OR\s+ALTER\s+)?FUNCTION\s", re.I),
    "CREATE TRIGGER": re.compile(r"^\s*CREATE\s+(OR\s+ALTER\s+)?TRIGGER\s", re.I),
    "CREATE TYPE": re.compile(r"^\s*CREATE\s+TYPE\s", re.I),
}
ANY_DDL = re.compile(r"^\s*(CREATE|ALTER)\s", re.I)


def read_text(path: Path) -> str:
    """SSMS writes UTF-16 LE with a BOM by default. Fall back to UTF-8."""
    raw = path.read_bytes()
    if raw[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return raw.decode("utf-16")
    return raw.decode("utf-8-sig")


def database_name(lines) -> str | None:
    """Identify the target database from the file's own content, not its filename."""
    for line in lines:
        m = re.match(r"\s*CREATE\s+DATABASE\s+\[([^\]]+)\]", line, re.I)
        if m:
            return m.group(1)
    for line in lines:
        m = re.search(r"/\*+ Object:\s+Database\s+\[([^\]]+)\]", line)
        if m:
            return m.group(1)
    for line in lines:
        m = re.match(r"\s*USE\s+\[([^\]]+)\]", line, re.I)
        if m and m.group(1).lower() != "master":
            return m.group(1)
    return None


def clean(lines):
    """Return (kept_lines, removed_after_cut, cut_index)."""
    cut = next((i for i, l in enumerate(lines) if FIRST_OBJECT in l), None)
    if cut is None:
        return None, None, None

    body = lines[cut:]
    kept, removed = [], []
    i = 0
    while i < len(body):
        line = body[i]
        s = line.strip().upper()

        # Anchored startswith: ALTER DATABASE can never match ALTER TABLE.
        is_db_stmt = (
            s.startswith("ALTER DATABASE")
            or s.startswith("CREATE DATABASE")
            or s.startswith("USE [")
            or s.startswith("USE ")
            and s.split()[1:2] and s.split()[1].startswith("[")
        )
        if is_db_stmt:
            # Consume the statement and its terminating GO. Handles wrapped
            # statements (e.g. a multi-line SET QUERY_STORE (...) block).
            removed.append(line)
            i += 1
            while i < len(body) and body[i].strip().upper() != "GO":
                removed.append(body[i])
                i += 1
            if i < len(body):                      # the GO itself
                removed.append(body[i])
                i += 1
            continue

        kept.append(line)
        i += 1

    # Trim blank lines at both ends; keep interior spacing untouched.
    while kept and not kept[0].strip():
        kept.pop(0)
    while kept and not kept[-1].strip():
        kept.pop()
    return kept, removed, cut


def write_into_placeholder(placeholder: Path, sql_lines) -> None:
    text = placeholder.read_text(encoding="utf-8")
    lines = text.splitlines()
    b = next(i for i, l in enumerate(lines) if MARKER_BEGIN in l)
    e = next(i for i, l in enumerate(lines) if MARKER_END in l)
    out = lines[: b + 1] + [""] + sql_lines + [""] + lines[e:]
    # LF endings, UTF-8 without BOM — matches .gitattributes (*.sql text eol=lf)
    placeholder.write_bytes(("\n".join(out) + "\n").encode("utf-8"))


def main() -> int:
    src_dir = REPO / (sys.argv[1] if len(sys.argv) > 1 else "SQL")
    placeholders = {p.name.split("_", 1)[1][:-4]: p for p in sorted(BASELINE_DIR.glob("*.sql"))}

    results, problems = [], []
    for src in sorted(src_dir.glob("*.sql")):
        lines = read_text(src).splitlines()
        db = database_name(lines)

        if db is None:
            problems.append(f"{src.name}: could not determine target database — SKIPPED")
            continue
        if db == "DevPrep_Statistics":
            problems.append(f"{src.name}: DevPrep_Statistics is out of scope (PR #78) — SKIPPED")
            continue
        if db not in placeholders:
            problems.append(f"{src.name}: database '{db}' matches no placeholder — SKIPPED")
            continue

        kept, removed, cut = clean(lines)
        if kept is None:
            problems.append(f"{src.name}: no '{FIRST_OBJECT}' marker found — SKIPPED")
            continue

        counts = {name: sum(1 for l in kept if rx.match(l)) for name, rx in COUNTERS.items()}
        unexpected = [
            l.strip()[:80] for l in kept
            if ANY_DDL.match(l) and not any(rx.match(l) for rx in COUNTERS.values())
        ]
        if counts["CREATE TABLE"] == 0:
            problems.append(f"{src.name}: ZERO CREATE TABLE statements in output")
        for u in unexpected:
            problems.append(f"{src.name}: unexpected DDL kept -> {u}")
        for r in removed:
            if r.strip():
                problems.append(f"{src.name}: removed after cut -> {r.strip()[:80]}")

        write_into_placeholder(placeholders[db], kept)
        results.append({
            "src": src.name, "db": db, "dst": placeholders[db].name,
            "total": len(lines), "stripped": len(lines) - len(kept),
            "kept": len(kept), "counts": counts,
        })

    w = max((len(r["src"]) for r in results), default=10)
    print(f"{'SOURCE'.ljust(w)}  {'-> PLACEHOLDER':<30} {'TOTAL':>6} {'STRIP':>6} {'KEPT':>6} "
          f"{'TBL':>4} {'IDX':>4} {'CONSTR':>7}")
    print("-" * (w + 72))
    for r in results:
        c = r["counts"]
        print(f"{r['src'].ljust(w)}  {r['dst']:<30} {r['total']:>6} {r['stripped']:>6} {r['kept']:>6} "
              f"{c['CREATE TABLE']:>4} {c['CREATE INDEX']:>4} {c['ALTER TABLE ADD CONSTRAINT']:>7}")

    print("\nOther statement types kept:")
    for name in ["ALTER TABLE ADD DEFAULT (unnamed)", "ALTER TABLE CHECK CONSTRAINT",
                 "CREATE VIEW", "CREATE PROCEDURE", "CREATE FUNCTION", "CREATE TRIGGER", "CREATE TYPE"]:
        total = sum(r["counts"][name] for r in results)
        if total:
            print(f"  {name}: {total}")

    print("\nFLAGS:" if problems else "\nFLAGS: none")
    for p in problems:
        print(f"  {p}")
    print(f"\nProcessed {len(results)} file(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
