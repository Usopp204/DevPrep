# `db/` — database bootstrap

## 1. Why this directory exists

The repository contained **no schema at all**. `grep -rl "CREATE TABLE" --include="*.sql"` returned nothing, so a fresh clone could not create the ten databases the services expect. Every database existed only as manually-applied state on individual developers' SQL Server instances.

This directory is **bootstrap convenience, not a migration framework.** Its single job: let `docker compose up` produce a working system on a machine that has never seen this project.

Two things it deliberately is **not**:

- **Not EF Migrations.** Schema is still hand-written SQL authored the way the team already authors it. Nothing here generates or applies EF migrations, and `dotnet ef` is not involved.
- **Not the place schema evolves.** Ongoing changes stay with their owning service (§4). These baselines are a point-in-time snapshot so a fresh volume can be populated; they are not re-generated on every change.

**Rule of thumb: a schema change that is not a committed `.sql` file does not exist.** That is precisely the failure this directory fixes, and it is easy to recreate by applying something in SSMS and forgetting to commit it.

## 2. SSMS export settings — configure once, then no hand-editing

Everything in the placeholders' "strip this" lists is controlled by an SSMS option. Set these once in **Generate Scripts → Set Scripting Options → Advanced** and the output is committable as-is.

**Object selection screen (before Advanced):** choose *Select specific database objects* and tick **Tables, Views, Stored Procedures, User-Defined Functions, User-Defined Types**. Leave **Users, Roles, Schemas, and Database Triggers** unticked — that alone removes most permission noise.

| Option | Set to | Prevents |
|---|---|---|
| **Script USE DATABASE** | **False** | the `USE [DevPrep_X]` header — init.sh targets the DB with `sqlcmd -d` |
| **Script Database Create** | **False** | `CREATE DATABASE` plus `ON PRIMARY (FILENAME = 'C:\...')` — absolute Windows paths that cannot work in a Linux container |
| **Script Extended Properties** | **False** | `EXEC sys.sp_addextendedproperty ...` noise. **Note: this one defaults to True** — the most commonly missed setting |
| **Script Collation** | **False** | `COLLATE SQL_Latin1_General_CP1_CI_AS` on every column, which pins the container to one dev box's collation |
| **Script Owner** | **False** | `ALTER AUTHORIZATION ON DATABASE::... TO [DOMAIN\user]` — references a Windows login that does not exist in the container |
| **Script Object-Level Permissions** | **False** | `GRANT` / `DENY` statements tied to logins that do not exist |
| **Script Statistics** | **Do not script statistics** | large, machine-specific, useless in a fresh database |
| **Script DROP and CREATE** | **Script CREATE only** | `DROP TABLE` statements that would destroy data on a re-run |
| **Types of data to script** | **Schema only** *(exceptions in §3)* | committing user data to a shared repository |
| **Script for Server Version** | **SQL Server 2022** | syntax the container's engine rejects |
| **Script Indexes** | **True** | — needed; indexes are part of the contract |
| **Script Primary Keys / Foreign Keys / Unique Keys** | **True** | — needed; structural |
| **Script Check Constraints / Defaults** | **True** | — needed; structural |
| **Script Triggers** | **True** | — needed if any exist |

**Option labels vary slightly between SSMS versions** (some read "Include…" rather than "Script…"). If a label does not match exactly, match on the description rather than the wording — and rely on the verification grep below rather than trusting the settings blind.

### What no option controls — leave these alone

- `SET ANSI_NULLS ON` / `SET QUOTED_IDENTIFIER ON` blocks before each procedure. Always emitted, harmless, required for correct procedure semantics.
- `GO` batch separators. **Keep them** — `sqlcmd` needs them to split batches.
- `ON [PRIMARY]` on table definitions. Harmless; the PRIMARY filegroup always exists.

### Verify before committing — must return nothing

```bash
grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|CREATE ROLE|GRANT |COLLATE |FILENAME *=|C:\\\\" db/baseline/*.sql
```

If this returns a hit, the corresponding option above was not set. **Fix the export and re-generate rather than hand-editing** — hand-edits do not survive the next regeneration.

## 3. Seed data — what the baselines actually carry

**As exported today, exactly one baseline contains data.** Verified by counting `INSERT`
statements in `db/baseline/*.sql`:

| Database | Rows in its baseline | Notes |
|---|---|---|
| `DevPrep_Question` | **34,776** — `Questions` 34,678, `QuestionCategories` 92, `Positions` 6 | The whole question bank, not just lookups. Drives the ~28 MB file size |
| all other nine | **0** | Schema only |

The admin UI's **"Tech Stacks" are `QuestionCategories` rows** — there is no `TechStacks` table.
`QuestionAPI/Repositories/TechStackRepository.cs:63-89` reads and writes `_db.QuestionCategories`,
and `QuestionBankDbContext` declares exactly three `DbSet`s, matching the three tables above.

Two things the baselines do **not** carry, despite earlier drafts of this section saying otherwise:

| Not in any baseline | Where it actually comes from |
|---|---|
| `DevPrep_Payment.CreditPackages` | `db/seed/08_DevPrep_Payment.sql`, only when `SEED_SAMPLE_DATA=true` |
| `DevPrep_Notification.EmailTemplates` | `NotificationAPI/Program.cs:61` seeds `VERIFY_EMAIL` itself at startup |

**If you regenerate `04_DevPrep_Question.sql`, script `Questions`, `QuestionCategories` and
`Positions` as "Schema and data".** Scripting only the lookup tables would drop 34,678 questions and
leave a stack that boots cleanly but cannot start an interview — the failure this note exists to
prevent.

**Never**: user rows, CVs, JDs, interview transcripts, evaluations, payment records. That is real personal data and must not enter git.

## 4. Where migrations live — they are NOT moving into `db/`

Ongoing schema changes stay with the service that owns the schema:

| Script | Current path | Destination | Tracked in git today? |
|---|---|---|---|
| `001_add_credit_ledger_bonus_index.sql` | `source/PaymentAPI/Sql/` | **unchanged** | ✅ yes |
| `002_add_credit_wallets_userid_unique_index.sql` | `source/PaymentAPI/Sql/` | **unchanged** | ❌ **no** — hidden by `.gitignore` |
| `011_add_hint_used_at.sql` | `source/InterviewAPI/Sql/` | **unchanged** | ❌ **no** |
| `012_add_last_skip_at.sql` | `source/InterviewAPI/Sql/` | **unchanged** | ❌ **no** |

They stay put because each service owns its own schema history — that is the database-per-service boundary the architecture is built on, and centralising migrations would blur it. `init.sh` reaches into `source/*/Sql/`; it does not relocate anything.

**What must change: `.gitignore` line 106, `/source/*/Sql/`.** It is why three of the four are invisible to a clean clone. Removing it, then `git add`-ing those three, is the deliberate commit — it is not done yet.

**Numbering gap worth five minutes:** the InterviewAPI scripts start at `011`, implying `001`–`010` existed and are in nobody's repo. If the baseline is generated from a database where those already ran, their effects are baked in and the gap is harmless. **Confirm that before assuming** — if the baseline comes from a database that never had them, `011`/`012` will fail against missing columns.

**Asymmetry to know about:** all four existing migrations begin with `USE [DevPrep_X];` selecting their own database, while the baselines deliberately have no `USE`. Both work — the migrations self-select, the baselines are targeted by `sqlcmd -d`. Do not "fix" one to match the other.

## 5. Execution order

No database depends on another at creation time (see §6), so the numbering is for determinism and readable logs, not dependency:

```
1. create all ten catalogs                     (init.sh — not written yet)
2. db/baseline/01..10_DevPrep_*.sql            (lexical order, each via sqlcmd -d)
3. source/<Service>/Sql/*.sql                  (lexical order, after that service's baseline)
```

Step 3 must follow step 2 **for the same database** — the incremental scripts assume their tables exist.

## 6. Running these against a fresh database

`db/init.sh` does this automatically inside the compose stack (see `infra/docker-compose.yml`'s `db-init` service). The manual equivalent, for running outside compose:

```powershell
# 1. create the catalogs (safe to re-run)
$dbs = "Auth","User","CvJd","Question","Interview","Evaluation","Feedback","Payment","News","Notification"
foreach ($d in $dbs) {
  sqlcmd -S localhost\MSSQLSERVER01 -Q "IF DB_ID('DevPrep_$d') IS NULL CREATE DATABASE [DevPrep_$d];"
}

# 2. apply baselines — note -d selects the target, the files contain no USE
Get-ChildItem db\baseline\*.sql | Sort-Object Name | ForEach-Object {
  $db = ($_.BaseName -split "_",2)[1]          # 01_DevPrep_Auth -> DevPrep_Auth
  sqlcmd -S localhost\MSSQLSERVER01 -d $db -i $_.FullName -b
}

# 3. apply each service's incremental scripts (these self-select via their USE line)
Get-ChildItem source\*\Sql\*.sql | Sort-Object FullName | ForEach-Object {
  sqlcmd -S localhost\MSSQLSERVER01 -i $_.FullName -b
}
```

`-b` makes `sqlcmd` exit non-zero on error, so a failure stops the loop instead of scrolling past.

**Idempotency is required.** Every file must be safe to re-run against an existing volume — wrap object creation in existence guards:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'InterviewSessions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.InterviewSessions ( ... );
END
```

`source/PaymentAPI/Sql/002_add_credit_wallets_userid_unique_index.sql` already demonstrates the house style — copy it so the codebase stays internally consistent.

### `db-init`'s idempotency is at the file level, driven by explicit completion markers

The baselines are raw SSMS output — bare `CREATE TABLE`/`INSERT`, no `IF NOT EXISTS` guards. Re-running one against a populated database fails on the first line that collides with an existing object or a unique-key row. So `db/init.sh` decides per file whether to run it at all.

> **Do not "simplify" this back to counting tables.** An earlier version inferred completion from schema state — if user tables existed, the baseline was considered done and skipped. That is precisely what let a **half-applied `DevPrep_Question` be skipped forever**: the run died partway through, left tables behind, and every retry saw those tables and moved on. The marker design below exists to make that impossible, and `init.sh` carries the same warning in its own header. Changing `init.sh` to match an older description of it would reintroduce the bug.

**Two markers, deliberately separate:**

| Table | Written after | Guards |
|---|---|---|
| `dbo.__baseline_applied` | `db/baseline/NN_*.sql` applies cleanly | the schema |
| `dbo.__seed_applied` | `db/seed/NN_*.sql` applies cleanly | the opt-in sample data |

They are separate so `SEED_SAMPLE_DATA` stays usable on a stack that is **already built** — a database whose baseline is long since marked complete is still eligible for seeding, so flipping the flag on later applies the seed without touching the schema or dropping the volume. One shared marker would have meant the seed could only ever land on a database created in the same run.

**The ordering is the whole point.** The marker row is inserted by a *separate statement, only after* the file's `sqlcmd` invocation returns 0 (`init.sh:193` for baselines, `:247` for seeds). Combined with `set -euo pipefail` and sqlcmd's `-b`, a SQL error aborts the container **before** any marker is written. A run that dies partway leaves **no marker**, even though it may have left tables or rows behind. Presence of tables is never treated as proof of completion.

**Three outcomes per file:**

1. **Marker present** (`init.sh:172`, `:239`) → skip, log `already marked complete` / `already seeded`.
2. **No marker, no user tables** → apply, then write the marker.
3. **No marker, but user tables exist** (`init.sh:182-188`) → **fail loudly**, with the message `has N table(s) but no completion marker — a previous run was interrupted mid-baseline. DROP DATABASE [X] and re-run; this cannot be resumed in place.` The baselines have no per-statement guards, so this genuinely cannot be resumed; failing beats a cryptic collision error or a silent skip.

Seed files are additionally guarded row-by-row with `IF NOT EXISTS` on their own keys, so even a lost `__seed_applied` marker cannot produce duplicates. That belt-and-braces is what made the `08_DevPrep_Payment.sql` retries safe after it failed twice mid-file.

**Practical consequence: on a second `docker compose up` against an existing volume, the 34,776 question-bank rows in `04_DevPrep_Question.sql` are not re-applied, even if you've since edited the baseline file.** (Payment credit packages and Notification email templates are unaffected — neither is in a baseline; see §3.) This is deliberate: it keeps re-runs fast and non-destructive. But it means a data fix landed in `db/baseline/04_DevPrep_Question.sql` after a volume already has `DevPrep_Question` populated will **not** reach that running instance. To pick it up, drop the affected database (or the whole `mssql-data` volume) so `db-init` sees no marker and re-applies the baseline in full.

## 7. Known limitation

These baselines reflect **one machine at one moment**. There is no authoritative schema to diff against — that absence is the root problem being fixed, and it also means the first version is only as correct as whichever machine produced it. Each service owner must verify their own database against the committed baseline; that ask is drafted in `docs/architecture/schema-recovery.md` §6.
