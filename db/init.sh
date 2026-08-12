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
# DATABASE level rather than the statement level: it counts user tables and skips
# any database that already has them. That keeps `docker compose up` on an
# existing volume a no-op without rewriting ten files of generated SQL.
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
run_sql()      { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -Q "$1"; }
run_sql_db()   { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -d "$1" -Q "$2"; }
run_file_db()  { "$SQLCMD" -S "$SERVER" -U "$SA_USER" -P "$MSSQL_SA_PASSWORD" $TLS -b -d "$1" -i "$2"; }

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

  # Statement-level idempotency does not exist in generated SSMS output, so guard
  # at the database level: if it already has user tables, this baseline already ran.
  count=$(run_sql_db "$db" "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;" -h -1 2>/dev/null | tr -d '[:space:]' || echo "")
  if [ -n "$count" ] && [ "$count" != "0" ]; then
    log "  skip ${base} — ${db} already has ${count} table(s)"
    skipped=$(( skipped + 1 ))
    continue
  fi

  log "  applying ${base} -> ${db}"
  run_file_db "$db" "$f"
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
