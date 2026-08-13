# Schema Recovery — the §1.1 TIER 3 item

**Why this document exists:** the repository contains **no schema**. `grep -rl "CREATE TABLE" --include="*.sql"` across the whole tree returns zero results. Nobody can rebuild DevPrep from a clean clone today — not a new teammate, not a grader, not us on a fresh machine. Every database currently exists only as manually-applied state on individual developers' SQL Server instances.

This is the one prerequisite that gates Phase 1 from *building* to *booting*, and the one item in it requiring team agreement.

---

## 1 — Standup pitch (three sentences, for TIER 3 approval)

> Right now nobody can rebuild DevPrep from a clean clone — there is no schema in the repo at all, so a fresh machine has no way to create the eleven databases the services expect. I want to script each database once from my local SQL Server via SSMS, commit those scripts under `db/baseline/`, and remove the `.gitignore` line that has been silently excluding our migration scripts from git. It stays hand-written SQL run by a container at startup — no EF migrations, no change to how we author schema — it just means `docker compose up` works on a machine that has never seen this project.

**If asked "why now":** containerization is blocked on it, and it is also the only thing standing between us and being able to hand the examiners a repo they can actually run.

**If asked "what's the risk":** the scripts come from my machine, so anyone whose local schema has drifted needs to verify their own — that ask is in the PR description (§6).

---

## 2 — Which databases get scripted

**Correction to the brief: there are 10 databases to script, not 11.** The count of 11 comes from `.env`, which still defines `STATISTICS_DB_CONNECTION_STRING` (`.env:7`) — but StatisticsAPI was deliberately removed by PR #78 four days ago (`git ls-tree -r dev | grep -i statisticsapi` → empty). `DevPrep_Statistics` is dead config; it is deleted in Phase 1 Batch I, not scripted.

Authoritative list = one `DbContext` per service, verified by `grep -rln ": DbContext" source`:

| # | Database | Owning service | DbContext | Script it? |
|---|---|---|---|---|
| 1 | `DevPrep_Auth` | AuthAPI | `Models/AuthDbContext.cs` | Yes |
| 2 | `DevPrep_User` | UserAPI | `Models/UserDbContext.cs` | Yes |
| 3 | `DevPrep_CvJd` | CvJdAPI | `Data/DocumentDbContext.cs` | **Yes — but see §4** |
| 4 | `DevPrep_Question` | QuestionAPI | `Data/QuestionBankDbContext.cs` | Yes |
| 5 | `DevPrep_Interview` | InterviewAPI | `Models/InterviewDbContext.cs` | Yes |
| 6 | `DevPrep_Evaluation` | EvaluationAPI | `Data/EvaluationDbContext.cs` | Yes |
| 7 | `DevPrep_Feedback` | FeedbackAPI | `Models/FeedbackDbContext.cs` | Yes |
| 8 | `DevPrep_Payment` | PaymentAPI | `Data/PaymentDbContext.cs` | Yes |
| 9 | `DevPrep_News` | NewsAPI | `Models/NewsDbContext.cs` | Yes |
| 10 | `DevPrep_Notification` | NotificationAPI | `Models/NotificationDbContext.cs` | Yes |
| — | ~~`DevPrep_Statistics`~~ | *removed PR #78* | none | **No — delete `.env:7`** |

---

## 3 — SSMS Generate Scripts settings

Per database: right-click → **Tasks → Generate Scripts → Select specific database objects**.

**Include:** Tables, Views, Stored Procedures, User-Defined Functions, User-Defined Types.
**Exclude:** Users, Roles, Schemas owned by logins, Database-level permissions.

**Advanced options — set these exactly:**

| Option | Value | Reason |
|---|---|---|
| Script Indexes | **True** | Indexes are part of the contract (see `001_add_credit_ledger_bonus_index.sql`) |
| Script Primary Keys / Foreign Keys / Unique Keys | **True** | Structural |
| Script Check Constraints / Defaults | **True** | Structural |
| Script Triggers | **True** | If any exist |
| Types of data to script | **Schema only** — except the lookup tables in §3.1 | Don't commit user data |
| Script USE DATABASE | **False** | The init container selects the DB itself; a `USE` in the file breaks reuse |
| Script Collation | **False** | Machine-specific; would pin the container to a dev box's collation |
| Script Database Create | **False** | `init.sh` creates the catalog |
| Script Owner / Authorization | **False** | Emits `ALTER AUTHORIZATION ... TO [DOMAIN\user]` — machine-specific and will fail in-container |
| Script Extended Properties | **False** | Noise |
| Script for Server Version | **SQL Server 2022** | Matches the container image |

### 3.1 — Data worth committing

> **Updated to match what was actually exported.** This section originally planned lookup rows for
> three databases. The export that landed differs, and the files are the authority — counted from
> `db/baseline/*.sql`, `04_DevPrep_Question.sql` holds **34,776 `INSERT`s** and the other nine hold
> **zero**. Credit packages and email templates are not in any baseline.

Schema-only for everything, **except** `DevPrep_Question`:

| Database | Script as | Contents |
|---|---|---|
| `DevPrep_Question` | **Schema and data** | `Questions` 34,678 rows, `QuestionCategories` 92, `Positions` 6 |
| the other nine | **Schema only** | — |

`Questions` is the question bank itself, not a lookup table — scripting only `QuestionCategories`
and `Positions` produces a stack that boots cleanly but cannot start an interview, because
`/internal/questions/export` returns `[]` and ai-service's RAG index seeds from it.

The admin UI's **"Tech Stacks" are `QuestionCategories` rows** — there is no `TechStacks` table
(`QuestionAPI/Repositories/TechStackRepository.cs:63-89` reads `_db.QuestionCategories`). Earlier
wording here said "tech stacks, positions" and meant exactly these two tables.

Not scripted, and deliberately so:
- `DevPrep_Payment` — credit packages live in `db/seed/08_DevPrep_Payment.sql`, behind `SEED_SAMPLE_DATA`
- `DevPrep_Notification` — `NotificationAPI/Program.cs:61` seeds `VERIFY_EMAIL` itself at startup

Everything else: schema only. **No user rows, no CVs, no interview transcripts** — that is real personal data and must not enter git.

---

## 4 — DECISION: `DevPrep_CvJd` is scripted like the other nine

**Status: decided. Do not revert without reading the failure mode below.**

### 4.1 Scope check — CvJdAPI is the only affected service

Verified across the whole solution:
```bash
grep -rnE "EnsureCreated(Async)?\(|Database\.Migrate(Async)?\(|MigrateAsync\(" source --include="*.cs" | grep -v /obj/ | grep -v /bin/
# → source/CvJdAPI/Program.cs:68:    db.Database.EnsureCreated();
```
**Exactly one hit.** No service calls `Migrate()`, `MigrateAsync()`, or `EnsureCreatedAsync()` anywhere. This matters because the two APIs fail differently against a pre-created database and would have needed separate handling in `init.sh`:

| Call | Against an existing-but-empty DB | Against an existing populated DB |
|---|---|---|
| `EnsureCreated()` | **Silently does nothing** — no tables created, service fails on first query | No-op (safe) |
| `Migrate()` | Applies pending migrations from `__EFMigrationsHistory` | Applies pending migrations |

Only the `EnsureCreated()` row is live here, so `init.sh` needs exactly one rule, not two. If anyone later adds `Migrate()` to a service, this section must be revisited — a `Migrate()` service needs `__EFMigrationsHistory` to exist and would conflict with a hand-scripted baseline.

### 4.2 The decision

**`init.sh` creates `DevPrep_CvJd` with its full schema, exactly like the other nine databases.**

### 4.3 The failure mode this avoids — spelled out so nobody "simplifies" it later

`source/CvJdAPI/Program.cs:68` runs `db.Database.EnsureCreated()` at startup. The critical, non-obvious property of that API:

> **`EnsureCreated()` creates the database *and* its tables — but only when the database does not already exist. If the database exists, it returns `false` and creates nothing, no matter how empty it is.**

So if `init.sh` were to create an *empty* `DevPrep_CvJd` (the natural thing to do for the other nine), the sequence would be:

1. `init.sh` runs `CREATE DATABASE DevPrep_CvJd` → empty database exists.
2. CvJdAPI starts, calls `EnsureCreated()`.
3. `EnsureCreated()` sees the database present, returns `false`, **creates no tables**.
4. CvJdAPI reports healthy — `/health` is a static literal (`Program.cs:53`) and never touches the DB.
5. The first real CV or JD request fails with "Invalid object name 'dbo.…'".

That failure is delayed, silent at startup, and invisible to the healthcheck — the worst combination to debug. **Creating the database empty is strictly worse than either alternative.**

### 4.4 Why (a) over the alternative

The rejected option was: exclude `DevPrep_CvJd` from `init.sh` and let `EnsureCreated()` own it end-to-end. That also works, but it leaves one database whose schema exists only in the EF model — invisible in git, un-inspectable without running the app, and inconsistent with the other nine. Since the entire point of this document is *"schema must be visible in the repo,"* carving out an exception to it defeats the exercise.

**Consequence of (a) to be aware of:** `EnsureCreated()` becomes permanently dead code — it will always find the database present and no-op. Leave the call in place (removing it is a CvJdAPI logic change and Duong's call, not required for containerization), but note it in `db/README.md` so nobody assumes it is still doing the work.

### 4.5 Guard against regression

Add to `db/README.md`:
> `DevPrep_CvJd` **must** be created with its full schema by `init.sh`. CvJdAPI calls `EnsureCreated()` (`Program.cs:68`), which creates nothing if the database already exists — so creating this database empty produces a service that starts healthy and then fails on the first query. See `docs/architecture/schema-recovery.md` §4.

---

## 5 — Committed layout, stripping, and execution order

### 5.1 Directory layout
```
db/
├── README.md                    # which option was chosen in §4; how to add a migration
├── init.sh                      # entrypoint for the db-init container
├── 00_create_databases.sql      # CREATE DATABASE x10, idempotent
└── baseline/
    ├── 000_DevPrep_Auth_baseline.sql
    ├── 000_DevPrep_User_baseline.sql
    ├── 000_DevPrep_CvJd_baseline.sql          # or omitted, per §4
    ├── 000_DevPrep_Question_baseline.sql
    ├── 000_DevPrep_Interview_baseline.sql
    ├── 000_DevPrep_Evaluation_baseline.sql
    ├── 000_DevPrep_Feedback_baseline.sql
    ├── 000_DevPrep_Payment_baseline.sql
    ├── 000_DevPrep_News_baseline.sql
    └── 000_DevPrep_Notification_baseline.sql
```
Incremental migrations stay where they already live — `source/<Service>/Sql/` — so each service keeps owning its own schema history. `init.sh` reaches into them; it does not relocate them.

### 5.2 What to strip from every generated file
SSMS emits machine-specific artifacts even with the options above. Grep each file before committing:

| Strip | Why |
|---|---|
| `USE [DevPrep_X]` / `GO` immediately after | `init.sh` targets the DB with `sqlcmd -d`; a `USE` makes the file non-reusable |
| `ALTER AUTHORIZATION ON DATABASE::... TO [...]` | References a Windows login that does not exist in the container |
| `CREATE USER` / `CREATE ROLE` / `GRANT` / `EXEC sp_addrolemember` | Login-specific; the container connects as `sa` |
| `COLLATE SQL_Latin1_General_CP1_CI_AS` on column definitions | Pins to the dev box's collation; the container's server default is correct |
| `CREATE DATABASE` + `ON PRIMARY (FILENAME = 'C:\...')` | Absolute Windows paths — guaranteed failure on Linux |
| `ALTER DATABASE ... SET ...` option blocks | Mostly defaults; noise, and some are edition-specific |
| `SET ANSI_NULLS/QUOTED_IDENTIFIER` repeated per object | Harmless but bloats diffs — collapse to one at the top |

**Verification grep before committing:**
```bash
grep -nE "USE \[|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME *=|C:\\\\" db/baseline/*.sql
# must return nothing
```

### 5.3 Idempotency
Every file must be safe to re-run against an existing volume. Wrap object creation:
```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'InterviewSessions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.InterviewSessions ( ... );
END
```
`source/PaymentAPI/Sql/002_add_credit_wallets_userid_unique_index.sql` already demonstrates this pattern — copy its style so the codebase stays internally consistent.

### 5.4 Execution order in `init.sh`
```
1. wait for SQL Server to accept connections   (retry loop, not a fixed sleep)
2. db/00_create_databases.sql                  (idempotent CREATE DATABASE x10)
3. db/baseline/000_*.sql                       (lexical order; each applied with sqlcmd -d <its db>)
4. source/*/Sql/*.sql                          (lexical order per service, after that service's baseline)
5. exit 0
```
Step 4 must run **after** step 3 for the same database — the incremental scripts assume their tables exist.

### 5.5 How the four existing incremental scripts fold in

| Script | Target DB | Tracked in git today? | Action |
|---|---|---|---|
| `source/PaymentAPI/Sql/001_add_credit_ledger_bonus_index.sql` | `DevPrep_Payment` | **Yes** | Leave as-is; already idempotent |
| `source/PaymentAPI/Sql/002_add_credit_wallets_userid_unique_index.sql` | `DevPrep_Payment` | **No** — `.gitignore:106` | `git add` after the ignore rule is removed |
| `source/InterviewAPI/Sql/011_add_hint_used_at.sql` | `DevPrep_Interview` | **No** | Same |
| `source/InterviewAPI/Sql/012_add_last_skip_at.sql` | `DevPrep_Interview` | **No** | Same |

**Ordering caution:** the InterviewAPI scripts are numbered `011` and `012`, implying `001`–`010` existed at some point and are **not in the repo or on disk**. If the baseline is generated from a database where those ten already ran, their effects are baked into the baseline and the gap is harmless. **Confirm that before assuming** — if the baseline comes from a database that never had them, `011`/`012` may fail against missing columns.

### 5.6 The `.gitignore` change
```diff
- # SQL migrations shared via Zalo, not tracked
- /source/*/Sql/
```
`.gitignore:105-106`. This is the line that has been silently keeping three of four migration scripts out of git. Removing it is the actual TIER 3 decision — everything else here follows from it.

After removal:
```bash
git status --porcelain source/*/Sql/     # should list the 3 previously-ignored scripts
git add source/InterviewAPI/Sql/ source/PaymentAPI/Sql/
```

---

## 6 — PR description note (drafted, for owners to verify their own database)

> **This PR adds the missing database schema so the project can be rebuilt from a clean clone.**
>
> Today there is no schema anywhere in the repo — `grep -rl "CREATE TABLE" --include="*.sql"` returns nothing. A fresh machine cannot create the ten databases the services expect, which blocks containerization and means a new teammate (or an examiner) can't run the project at all.
>
> **What changed:** ten baseline scripts under `db/baseline/`, generated by hand from SQL Server via SSMS Generate Scripts — schema only for nine of them, and schema **plus the full question bank** (34,776 rows across `Questions`, `QuestionCategories`, `Positions`) for `04_DevPrep_Question.sql`. Credit packages and email templates are *not* in the baselines: the former is in `db/seed/`, the latter is seeded by NotificationAPI at startup. Plus `db/init.sh`, which a one-shot container runs at startup. Plus removal of `.gitignore:106` (`/source/*/Sql/`), which had been excluding three of our four existing migration scripts from git.
>
> **We are not switching to EF migrations.** Schema is still hand-written SQL that we author the same way we always have. The only change is that the scripts now live in the repo and a container applies them, instead of each of us applying them manually in SSMS.
>
> **⚠ What I need from each of you — please verify your own database.**
> These scripts were generated from **my** local SQL Server. If your schema has drifted from mine — a column you added locally and applied by hand, an index you created while debugging, anything you ran in SSMS but didn't share — **it will not be in these files**, and the containerized stack will be missing it.
>
> Please check the baseline for the database(s) you own and reply with either "matches" or a diff:
>
> | Database | Owner to verify |
> |---|---|
> | `DevPrep_Auth` | *(see ownership table — split, please both check)* |
> | `DevPrep_User` | *(split)* |
> | `DevPrep_Payment`, `DevPrep_CvJd` | Duong |
> | `DevPrep_Question` | *(split — Duy / HuynhAnh / Duyen)* |
> | `DevPrep_Notification`, `DevPrep_News` | HaoNam |
> | `DevPrep_Feedback` | *(split — HaoNam / Usopp204)* |
> | `DevPrep_Interview`, `DevPrep_Evaluation` | Duyen (me) |
>
> Quickest check: `docker compose up -d`, then compare your service's tables against your local DB — or just open the baseline file and scan for anything you know you added that isn't there.
>
> **Known open item:** `DevPrep_CvJd` is special — CvJdAPI calls `EnsureCreated()` (`CvJdAPI/Program.cs:68`), which only builds tables when the database doesn't already exist. See `docs/architecture/schema-recovery.md` §4 for the two options; I've gone with **(a) script it like the others** unless Duong prefers otherwise.

---

## 7 — Honest limitations

1. **Baselines reflect my machine at one moment.** There is no authoritative schema to diff against — that is the root problem this fixes, and it also means the first version is only as correct as my local state. §6 is the mitigation; it depends on people actually checking.
2. **Drift is invisible until it bites.** After this lands, any schema change applied in SSMS but not committed reintroduces exactly the problem being fixed. `db/README.md` should say plainly: *a schema change that isn't a committed `.sql` file does not exist.*
3. **The `011`/`012` numbering gap is unexplained** (§5.5) and worth five minutes before assuming the baseline covers it.
4. **Seed data selection is a judgment call.** The proposal was tech stacks, positions, credit packages, and email templates; what actually shipped differs — see §3.1. The `DevPrep_Question` baseline carries the full question bank (34,776 rows, not just lookups), while credit packages moved to `db/seed/` behind `SEED_SAMPLE_DATA` and email templates are seeded by NotificationAPI at startup. If a service needs other lookup rows to boot cleanly, its owner is the only one who knows.
