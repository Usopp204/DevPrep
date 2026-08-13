# `db/seed/` — sample data (opt-in, not schema)

Sign-in accounts, credit packages and starting credits, so a fresh clone is usable
without manual setup.

**This is sample data, not structure.** `db/baseline/` is the schema and always runs;
this directory only runs when you ask for it. Keeping them apart is the point — one is
what the system *is*, the other is convenience for local work.

---

## 1. Enable it

**Two steps. The first is not optional — the seed aborts without it.**

### Step 1 — generate the two password hashes

`db/seed/01_DevPrep_Auth.sql` ships with placeholder hashes and a `THROW` that stops the
whole seed if they are still there. That is deliberate: a half-applied seed that creates
accounts nobody can sign into is worse than a loud failure.

Run this **three times** — once per account. Docker is the only requirement:

```powershell
docker run --rm python:3.12-alpine sh -c "pip install --quiet bcrypt && python -c \"import bcrypt; print(bcrypt.hashpw(b'DevPrep@Local1', bcrypt.gensalt(12, prefix=b'2a')).decode())\""
```

Each run prints one 60-character string starting with `$2a$12$`. Paste them into
`01_DevPrep_Auth.sql`:

| Run | Variable |
|---|---|
| 1st | `@AdminPasswordHash` |
| 2nd | `@ManagerPasswordHash` |
| 3rd | `@CandidatePasswordHash` |

Why these exact parameters: `AuthAPI/Services/BCryptPasswordHasher.cs` uses
`BCrypt.Net.BCrypt.HashPassword(password, 12)`, which produces a `$2a$` hash at work
factor 12. `prefix=b'2a'` matches it exactly — Python's bcrypt otherwise emits `$2b$`,
and while BCrypt.Net-Next reads that too, matching removes the question.

Running it three times gives different salts for the same password, which is normal and
correct. Reusing one hash for all three would also work.

### Step 2 — turn the flag on

```
# infra/.env.docker
SEED_SAMPLE_DATA=true
```

```powershell
cd infra
docker compose --env-file .env.docker up db-init
```

---

## 2. Does this work on a stack I have already built?

**Yes. You do not need to drop the volume.**

This is worth stating plainly because the schema behaves differently. `db-init` is
idempotent per database via a completion marker, and the seed has **its own separate
marker**:

| Marker table | Written by | Meaning |
|---|---|---|
| `dbo.__baseline_applied` | `db/baseline/*.sql` | schema for this database is complete |
| `dbo.__seed_applied` | `db/seed/*.sql` | sample data for this database is loaded |

Because they are separate, a database whose **baseline is already marked complete is
still eligible for seeding**. Turning `SEED_SAMPLE_DATA=true` on an existing stack and
re-running `db-init` applies the seed and leaves the schema untouched.

If the seed had shared the baseline's marker, it could only ever have landed on a
database created in the same run, and enabling the flag later would silently do nothing.

**Re-running with the flag already on is a no-op** — the marker skips it, and every row
is additionally guarded by `IF NOT EXISTS`, so even a lost marker cannot duplicate data.

---

## 3. What you get

| Account | Email | Role | Credits |
|---|---|---|---|
| Admin | `admin@local.devprep.test` | `Admin` | none |
| Manager | `manager@local.devprep.test` | `Manager` | none |
| Candidate | `candidate@local.devprep.test` | `User` | 10,000 |

**Password for all three: `DevPrep@Local1`**

This password is public — it is written here, in the repo, on purpose. It is not a
secret and must never be used anywhere real. Three things keep it contained: the seed is
off by default, every address contains `local`, and `.test` is reserved by RFC 2606 so
none of them can ever be a deliverable email address.

**Neither Admin nor Manager gets credits, deliberately.** WebClient redirects *both*
roles away from Buy Credits (`PaymentController.cs:19-20, 32-33`) and Credit History
(`ProfileController.cs:240-243`), so a balance on those accounts would be data they can
never see or spend. Adding one for symmetry would just be misleading.

10,000 credits = 83 sixty-minute interviews, or 333 fifteen-minute ones (2 credits per
minute, per `InterviewAPI/Helpers/CreditConfig.cs`) — enough for repeated testing rather
than one demo run. It is a single ledger row, which is what keeps `BalanceAfter`
provably equal to the running `SUM(CreditChange)`.

**Credit packages** — exactly three, matching the same pricing model:

| Name | Credits | Bonus | Price | Buys |
|---|---|---|---|---|
| Starter | 100 | 0 | 49,000 ₫ | 1× 60-min |
| **Professional** | 250 | 50 | 99,000 ₫ | 2× 60-min + 1× 30-min |
| Enterprise | 600 | 150 | 199,000 ₫ | 6× 60-min |

Three active rows is required, not stylistic: `Views/Payment/Index.cshtml:9-10` shows the
first three and only highlights a featured tier when the count is exactly three.

---

## 4. What the seed does NOT do

It writes `Accounts` directly instead of going through registration, so seeded accounts
have **no `LoginAudits` row** and **no `EmailVerificationTokens` row**; `IsEmailVerified`
is set to `1` on the account instead of being earned through the verify endpoint.

Harmless today — `AuthService.LoginCoreAsync` gates only on `Status`, `LockedUntil`,
`PasswordHash`, `IsEmailVerified` and `Role`, and never reads either table. Both are
append-only history. It would stop being harmless if login ever started *requiring* a
prior verification token or audit row.

---

## 5. Removing it

Every line is marked. One search finds all of it:

```bash
grep -rn "SAMPLE-DATA" db/ infra/
```

| File | Action |
|---|---|
| `db/seed/` | delete the directory |
| `db/init.sh` | delete the block between the `SAMPLE-DATA:` markers (section 3b) |
| `infra/docker-compose.yml` | delete the marked `SEED_SAMPLE_DATA` env line on `db-init` |
| `infra/.env.docker.example` | delete the marked block |
| `infra/.env.docker` | remove `SEED_SAMPLE_DATA` (untracked, so grep will not find it on a clean clone — but it exists on any machine that ran the seed) |

Deleting the files does **not** remove rows already inserted. To clear those:

```sql
DELETE FROM DevPrep_Payment.dbo.CreditLedger    WHERE CAST(LedgerId  AS char(36)) LIKE '5EED%';
DELETE FROM DevPrep_Payment.dbo.CreditPackages  WHERE CAST(PackageId AS char(36)) LIKE '5EED%';
DELETE FROM DevPrep_User.dbo.UserProfiles       WHERE CAST(AccountId AS char(36)) LIKE '5EED%';
DELETE FROM DevPrep_Auth.dbo.Accounts           WHERE CAST(AccountId AS char(36)) LIKE '5EED%';
DROP TABLE DevPrep_Auth.dbo.__seed_applied;
DROP TABLE DevPrep_User.dbo.__seed_applied;
DROP TABLE DevPrep_Payment.dbo.__seed_applied;
```

Delete in that order — `Accounts` last, since the other rows reference its ids.

**Every seeded primary key starts with `5EED`** ("SEED"), which is what makes those
`LIKE` filters exact rather than a guess. The same id also links the three databases: the
candidate's `Accounts.AccountId` is the `UserProfiles.AccountId` and the
`CreditLedger.UserId`. There is no separate user identity in this system.
