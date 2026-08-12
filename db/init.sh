#!/usr/bin/env bash
#
# init.sh — create the ten DevPrep databases and apply their baseline schema.
#
# Runs as a one-shot container against a fresh SQL Server on an empty volume.
# Every application service must wait for this to exit 0 before starting: F-01's
# cross-database queries (PaymentAPI and UserAPI read DevPrep_Auth, DevPrep_User,
# DevPrep_Interview and DevPrep_Payment with three-part names) mean a service can
# fail at runtime if ANY of the ten is missing, not just its own. Compose enforces
# that with `depends_on: db-init: {condition: service_completed_successfully}`.
#
# There is no creation-order dependency BETWEEN the ten — SQL Server does not
# support cross-database foreign keys, so nothing here can reference another
# database at DDL time. The numeric order is for deterministic, readable logs.
#
# IDEMPOTENCY — read this before changing anything.
# The baselines are raw SSMS output: bare `CREATE TABLE` with no IF NOT EXISTS
# guards. Re-running them against a populated database fails with "There is
# already an object named ...". This script is therefore idempotent at the
# DATABASE level rather than the statement level, using an explicit completion
# marker rather than inferring completion from schema state.
#
# Each database gets a dbo.__baseline_applied table with exactly one row once
# its baseline has run to completion. That row is inserted in a SEPARATE
# statement, only after run_file_db returns 0 — so a run that dies partway
# through (as 04_DevPrep_Question.sql's sqlcmd $(...) failure once did) leaves
# NO marker, even though it may have left tables behind. Presence of tables is
# deliberately NOT treated as proof of completion: that was the previous
# design, and it is exactly what let a half-applied DevPrep_Question go
# silently skipped forever on retry.
#
# If a database has tables but no marker, a prior run was interrupted. The
# baselines have no per-statement idempotency guards, so that cannot be
# resumed in place — the check below fails loudly with instructions to
# DROP DATABASE rather than attempting (and cryptically failing) a re-apply.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASELINE_DIR="${SCRIPT_DIR}/baseline"
SERVER="${SQLSERVER_HOST:-sqlserver}"
SA_USER="sa"
WAIT_TIMEOUT_SECONDS="${WAIT_TIMEOUT_SECONDS:-120}"

DATABASES=(
  DevPrep_Auth
  DevPrep_User
  DevPrep_CvJd
  DevPrep_Question
  DevPrep_Interview
  DevPrep_Evaluation
  DevPrep_Feedback
  DevPrep_Payment
  DevPrep_News
  DevPrep_Notification
)

log()  { printf '[db-init] %s\n' "$*"; }
fail() { printf '[db-init] ERROR: %s\n' "$*" >&2; exit 1; }

# --- Preconditions ----------------------------------------------------------
[ -n "${MSSQL_SA_PASSWORD:-}" ] || fail "MSSQL_SA_PASSWORD is not set. If you ran docker compose without --env-file .env.docker, that is why."
[ -d "$BASELINE_DIR" ] || fail "baseline directory not found at ${BASELINE_DIR}"

# mssql-tools installs to /opt/mssql-tools; mssql-tools18 to /opt/mssql-tools18 and
# requires -C to trust the server's self-signed certificate.
if   [ -x /opt/mssql-tools18/bin/sqlcmd ]; then SQLCMD=/opt/mssql-tools18/bin/sqlcmd; TLS="-C"
elif [ -x /opt/mssql-tools/bin/sqlcmd   ]; then SQLCMD=/opt/mssql-tools/bin/sqlcmd;   TLS=""
elif command -v sqlcmd >/dev/null 2>&1;    then SQLCMD="$(command -v sqlcmd)";        TLS="-C"
else fail "sqlcmd not found (looked in /opt/mssql-tools18, /opt/mssql-tools, PATH)"
fi
log "using ${SQLCMD}"

# -b makes sqlcmd exit non-zero on a SQL error, which combined with `set -e` is what
# turns a failed script into a failed container instead of a half-built database.
# -x disables sqlcmd's own $(variable_name) scripting-variable substitution. sqlcmd
# scans raw input text for that pattern before parsing SQL, with no awareness of
# string-literal quoting — a literal "$(...)" inside seed-data text (e.g. jQuery
# examples like "$(this)" or "$(document).ready()" in 04_DevPrep_Question.sql) gets
# treated as a variable reference and aborts with a client-side syntax error, even
# though the SQL itself is well-formed. None of our scripts use sqlcmd variables, so
# this substitution has no legitimate use here and is off everywhere.
run_sql()      { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -x -Q "$1"; }
run_file_db()  { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -x -d "$1" -i "$2"; }
# For a single-row single-column result only. -h -1 drops the header AND the dashed
# separator line under it; -W right-trims column padding. These flags are baked into
# this function's own definition, not appended as extra arguments at a call site: a
# fixed-arity shell function silently ignores any argument past the ones it names, so
# passing "-h -1" as a 3rd/4th argument to a two-parameter function does nothing —
# not an error, just silently discarded. That was this script's original bug: sqlcmd
# ran with default formatting (header + separator + value + footer) the whole time,
# and the flags meant to fix that never reached it at all.
query_scalar_db() { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -x -d "$1" -h -1 -W -Q "$2"; }
# Same as run_sql but scoped to one database via -d, for inline statements with
# no result set to capture (creating/inserting into the completion marker).
run_sql_db()   { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -x -d "$1" -Q "$2"; }

# --- 1. Wait for SQL Server -------------------------------------------------
# The container reports ready before the engine accepts logins, so poll rather
# than sleeping a fixed amount.
log "waiting for SQL Server at ${SERVER} (timeout ${WAIT_TIMEOUT_SECONDS}s)..."
deadline=$(( SECONDS + WAIT_TIMEOUT_SECONDS ))
until run_sql "SELECT 1" >/dev/null 2>&1; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    fail "SQL Server did not accept connections within ${WAIT_TIMEOUT_SECONDS}s. Check: is MSSQL_SA_PASSWORD at least 8 chars with complexity? The image refuses to start otherwise."
  fi
  sleep 2
done
log "SQL Server is accepting connections."

# --- 2. Create the ten databases -------------------------------------------
# CREATE DATABASE lives here, not in the baselines: SSMS emits it with absolute
# Windows FILENAME paths that cannot work on Linux. Letting the engine choose its
# own default data/log paths is the whole point.
log "creating databases (idempotent)..."
for db in "${DATABASES[@]}"; do
  run_sql "IF DB_ID('${db}') IS NULL BEGIN CREATE DATABASE [${db}]; END" >/dev/null
  log "  ok  ${db}"
done

# --- 3. Apply baselines in numeric order ------------------------------------
shopt -s nullglob
baselines=( "${BASELINE_DIR}"/*.sql )
shopt -u nullglob
[ "${#baselines[@]}" -gt 0 ] || fail "no baseline files found in ${BASELINE_DIR}"

applied=0; skipped=0
for f in "${baselines[@]}"; do
  base="$(basename "$f")"
  # 01_DevPrep_Auth.sql -> DevPrep_Auth
  db="${base#*_}"; db="${db%.sql}"

  case " ${DATABASES[*]} " in
    *" ${db} "*) ;;
    *) fail "baseline ${base} maps to '${db}', which is not one of the ten databases" ;;
  esac

  # Completion marker, not table count: ensure dbo.__baseline_applied exists,
  # then use its row count (0 or 1) to decide whether this baseline already ran
  # to completion. No `|| echo ""` fallback here on purpose: if a query fails,
  # that is a real problem (permissions, connectivity) and must stop the
  # script, not be silently treated as "not complete, safe to proceed."
  run_sql_db "$db" "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'__baseline_applied' AND schema_id = SCHEMA_ID('dbo')) BEGIN CREATE TABLE dbo.__baseline_applied (applied_at DATETIME2 NOT NULL); END" >/dev/null

  raw_marker="$(query_scalar_db "$db" "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.__baseline_applied;")"
  marker_count="$(printf '%s' "$raw_marker" | tr -d '[:space:]')"
  if ! [[ "$marker_count" =~ ^[0-9]+$ ]]; then
    fail "could not read completion marker for ${db}: sqlcmd returned $(printf '%q' "$raw_marker") (stripped to $(printf '%q' "$marker_count")). Expected a bare integer from -h -1 -W."
  fi
  if [ "$marker_count" -gt 0 ]; then
    log "  skip ${base} — ${db} already marked complete"
    skipped=$(( skipped + 1 ))
    continue
  fi

  # Marker says "not complete." If user tables already exist anyway (other than
  # the marker table itself), a previous run was interrupted mid-baseline.
  # Re-applying would fail on the first colliding CREATE TABLE with a
  # confusing raw SQL error — fail here instead, with an actionable message.
  raw_objcount="$(query_scalar_db "$db" "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'__baseline_applied';")"
  objcount="$(printf '%s' "$raw_objcount" | tr -d '[:space:]')"
  if ! [[ "$objcount" =~ ^[0-9]+$ ]]; then
    fail "could not read object count for ${db}: sqlcmd returned $(printf '%q' "$raw_objcount")."
  fi
  if [ "$objcount" -gt 0 ]; then
    fail "${db} has ${objcount} table(s) but no completion marker — a previous run was interrupted mid-baseline. DROP DATABASE [${db}] and re-run; this cannot be resumed in place."
  fi

  log "  applying ${base} -> ${db}"
  run_file_db "$db" "$f"
  run_sql_db "$db" "INSERT INTO dbo.__baseline_applied (applied_at) VALUES (SYSDATETIME());" >/dev/null
  applied=$(( applied + 1 ))
done

# --- 4. Verify ---------------------------------------------------------------
log "verifying all ten databases exist..."
missing=0
for db in "${DATABASES[@]}"; do
  if ! run_sql "IF DB_ID('${db}') IS NULL RAISERROR('missing',16,1);" >/dev/null 2>&1; then
    printf '[db-init] ERROR: %s is missing after init\n' "$db" >&2
    missing=$(( missing + 1 ))
  fi
done
[ "$missing" -eq 0 ] || fail "${missing} database(s) missing — services must not start"

log "done. applied=${applied} skipped=${skipped} databases=${#DATABASES[@]}"

# --- NOT APPLIED HERE: source/<Service>/Sql/*.sql ---------------------------
# The four existing migration scripts are deliberately NOT applied. Verified: their
# effects are already present in the baselines, because the export was taken from
# databases where they had already run —
#     011_add_hint_used_at.sql          -> SessionQuestions.HintUsedAt      present
#     012_add_last_skip_at.sql          -> InterviewSessions.LastSkipAtUtc  present
#     001_add_credit_ledger_bonus_index -> UX_Ledger_UserBonus              present
#     002_add_credit_wallets_userid_... -> UX_CreditWallets_UserId          present
# Three of the four are also still excluded by .gitignore, so a clean clone does not
# even have them — applying them here would make the result differ between machines,
# which is the exact problem this directory exists to remove.
#
# Migrations authored AFTER this baseline snapshot WILL need applying. When that
# happens, uncomment the loop below and drop the applied migration's effects from
# the next baseline regeneration:
#
#   for f in "${SCRIPT_DIR}"/../source/*/Sql/*.sql; do
#     [ -e "$f" ] || continue
#     log "  migration $(basename "$f")"
#     "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -i "$f"
#   done
#
# Those scripts carry their own `USE [DevPrep_X];` line, so they are run without -d.
exit 0
