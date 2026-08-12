# DevPrep Microservice Remediation Plan

**Companion to:** `docs/architecture/microservice-audit.md` (current score **11/36**). Finding IDs (F-01 … F-17) refer to that document, except **F-17**, defined here.
**Status:** planning only. No source file was modified while producing this plan.
**Target** (Section F Q16): *microservice-correct where the board can verify it* — full containerization, database-per-service, one real async flow, basic observability, Critical security findings closed. Not in scope: per-service CI/CD, repository splitting.
**Projected score: 11/36 → ~30/36.**

---

## 0.0 — Freeze verification (read before relying on direct-edit rights)

The team's statement — *"all feature code is complete and frozen; only Docker standardization remains"* — was checked against git on 2026-08-11. **Verdict: consistent.** No service contradicts it outright. Four caveats follow, none of which invalidate the premise but three of which change *who gets notified*.

**Supporting evidence:**
- **No in-flight work.** Every branch except four is at `ahead-of-dev: 0` — fully merged. The four exceptions are all stale: `wip/pause-cheating-ui-checkpoint` (2 weeks, mine), `origin/fix/idle-timer-reset` (4 weeks, mine), `origin/revert-17-feature/manager-users` (6 weeks), `origin/feature/login-ui` (7 weeks, `ho-thanh-duy`). None represent active development.
- **Duong's most recent work is merged.** `update cvjd logic` and `update logic credit` (3 days ago) both verified as ancestors of `dev` via `git merge-base --is-ancestor`.
- **Clean tree.** `git status` shows no modified or staged source files — only untracked docs/report artifacts. **`git stash list` is empty.**
- **No fresh TODO debt.** Zero `TODO`/`FIXME`/`HACK` lines added to any teammate service in 14 days. Current totals repo-wide: QuestionAPI 1, InterviewAPI 3 (mine).

**Caveat 1 — the freeze is ~16 hours old, and its last act touched all seven teammate services at once.** `git log -1 -- source/<svc>` returns the *same* commit for AuthAPI, UserAPI, PaymentAPI, QuestionAPI, NotificationAPI, NewsAPI, and CvJdAPI: HaoNam, 16 hours ago, from `fix/security-p0-internal-api` (PR #81). A cross-cutting change landing across every service I am about to edit, that recently, argues for pulling `dev` immediately before starting and for doing Phase 1 in small commits rather than one large one.

**Caveat 2 — those commits have corrupted messages.** Three consecutive commits from that PR have the literal subject `@'` — a PowerShell here-string leaking into `git commit -m`. Not a blocker and not my code to fix, but it signals the work landed in a hurry, which slightly weakens "complete and frozen" as a statement about *care taken*, not about *volume of work remaining*.

**Caveat 3 — the ownership model omits two active contributors.** Last 30 days by commit count: DuyenTQ2003 41, **Duong Nguyen 28**, **ho-thanh-duy 24**, **Usopp204 24**, DHHaoNam 16, HaoNam 10, **HuynhAnh181405 8**, Duyen Tran (Daniel) 8. The TIER 2 notify list names only Usopp204, DHHaoNam, and Duong — but `ho-thanh-duy` is the third-most-active contributor overall, and `HuynhAnh181405` committed *auth validation and admin user service* changes 3 days ago. **If I edit AuthAPI or UserAPI, HuynhAnh181405 may be the right person to notify, not the three listed.** Confirm before sending summaries.

**Caveat 4 — `DHHaoNam` and `HaoNam` are almost certainly one person with two git configs** (they appear as separate authors, and `DHHaoNam` merges PRs authored by `HaoNam`). One notification, not two.

**Correction to the audit, discovered during this check:** F-04's framing was wrong. StatisticsAPI was not an unbuilt stub whose docs drifted — it was **deliberately removed** by PR #78, `refactor(dashboard): remove StatisticsAPI and aggregate metrics live`, 4 days ago. `git ls-tree -r dev | grep -i statisticsapi` is empty: it is already gone from `dev`. What remains on my disk is untracked `bin/`/`obj/` residue. F-04 is therefore *docs not updated after an intentional removal* — a smaller, purely documentary item. See Phase 8. (`.env:7` still defines a now-dead `STATISTICS_DB_CONNECTION_STRING`.)

---

## 0.1 — Ownership model (replaces the previous MINE / NEEDS-TEAMMATE / SHARED buckets)

### TIER 1 — Infrastructure. Edit freely, no notice needed.
Touches no business logic. Dockerfiles, `docker-compose.yml`, `.dockerignore`, connection strings moving to env vars, `localhost:<port>` → compose service names, port-exposure removal, SQL Server password change, `.env`/`.env.example` restructuring.

> **One judgment call flagged:** `UserAPI/Program.cs:49,54,73` are `localhost:<port>` literals that happen to live in C# rather than JSON. They fall under TIER 1 by your definition ("localhost:<port> becoming compose service names") and contain no business logic. I have classified them TIER 1 **but still log them in the change log below**, because a `.cs` diff in someone else's service is worth a courtesy line even when the rule doesn't demand one.

### TIER 2 — Logic changes in teammate services. Edit directly, notify the owner after.
Each change gets a one-line change-log entry (§0.2) so owners receive a summary rather than a permission request.

Explicitly listed by you: **F-05** (`X-User-Id` → JWT subject), **F-15** (CvJdAPI auth), **CvJdAPI's missing `X-Internal-Api-Key` header**, **F-01** (cross-database joins → internal API calls).

> **Items your tier definitions do not cover — my proposed classification, flagged rather than assumed.** Each is a logic change in a teammate service, so TIER 2 by analogy, but none appear in your list:
> - **F-02** — QuestionAPI fail-open internal auth (`InternalQuestionController.cs:40-45`) + fail-closed startup guard.
> - **Phase 2 middleware** — correlation-ID middleware and real readiness checks in the 7 teammate services' `Program.cs`.
> - **Phase 5** — new PaymentAPI migration `003_credit_ledger_reference_unique.sql`, `InternalCreditService` duplicate handling, and the orphan-account sweep in AuthAPI or UserAPI.
>
> If any of these should be TIER 3 instead, say so — I have not started them.

### TIER 3 — Still requires team agreement. Changes how everyone works.
- Removing `.gitignore:106` (`/source/*/Sql/`) and **committing schema scripts**
- **Centralizing JWT validation at the gateway**
- **Adding RabbitMQ**

### 0.2 — TIER 2 change log (populate as changes land; send as a summary)

| # | File:line | What changed | Why | Owner to notify |
|---|---|---|---|---|
| *(pending)* | `PaymentAPI/Program.cs:235-251` | `/credits/balance` takes identity from JWT subject, not `X-User-Id`; added `/api/internal/credit/balance` for service callers | F-05 — auth bypass on a financial endpoint | PaymentAPI owner *(confirm — git suggests Duong Nguyen)* |
| *(pending)* | `CvJdAPI/Program.cs:22-25` | `"ai"` HttpClient now sends `X-Internal-Api-Key` | Prerequisite for F-17: lets ai-service authenticate `/parse/*` | Duong |
| *(pending)* | `CvJdAPI/Program.cs`, `Endpoints/*` | Added JWT bearer auth + `RequireAuthorization()` | F-15 — unauthenticated GPU access | Duong |
| **DONE (Batch E)** | `CvJdAPI/appsettings.json` (added), `appsettings.Development.json` (key removed), 4 call sites | `AI_SERVICE_URL` corrected to 8080; literal fallbacks replaced with a clear error | §0(c) — CV/JD broken in Docker mode | Duong — **notify only, not an ask**; message at `team-notifications.md` §0.11 |
| *(pending)* | `PaymentAPI/Program.cs:456-460`, `ManagerPaymentsController.cs:210-215` | Cross-DB joins → batch internal API calls | F-01 | PaymentAPI owner |
| *(pending)* | `UserAPI/Controllers/ManagerUsersController.cs:59-266` | Cross-DB joins → batch internal API calls | F-01 | Usopp204 *(or HuynhAnh181405 — see Caveat 3)* |
| *(pending)* | `QuestionAPI/Controllers/InternalQuestionController.cs:40-45` | Removed fail-open branch; startup now fails closed | F-02 *(unlisted — see flag above)* | DHHaoNam/HaoNam |
| *(courtesy)* | `UserAPI/Program.cs:49,54,73` | Hardcoded peer URLs → config | TIER 1 by rule; logged because it is a `.cs` edit | Usopp204 |

---

## 0 — Out-of-band items

Still listed outside the phases because they are security defects or a broken flow. **What changed:** these are now TIER 2 — I fix them directly and notify after, rather than waiting on a hand-off.

### (a) F-05 — Unauthenticated `X-User-Id` trust on `/credits/balance` — **Critical severity, local-only reachability**
`PaymentAPI/Program.cs:235-251`: identity from a client header, no `[Authorize]`, not covered by the `/api/internal` middleware (`:151`). Reachability confirmed local-only — the cloudflared tunnel fronts ai-service:8080, not ApiGateway:7000 `[CONFIRMED A-3]`. Severity stays Critical; urgency is "this week," not "same day."

**Fix (TIER 2, mine to make, ~30 min)** `[ASSUMED A-2]`: `[Authorize]` + token-subject identity on `/credits/balance`; add `/api/internal/credit/balance` inheriting the fail-closed middleware (`:139-169`); repoint `InterviewAPI/Clients/PaymentApiClient.cs:164-181`. Both halves are now mine — **no longer a two-party change.**

### (b) F-15 — CvJdAPI has no authentication on any endpoint
`CvJdAPI/Program.cs` (73 lines): no `AddAuthentication`/`AddAuthorization`; endpoints at `:61-63` unguarded; 5-minute AI timeout at `:22-25`. **Fix (TIER 2, ~1 h):** copy the JWT block from `InterviewAPI/Program.cs:115-118`, add `RequireAuthorization()`. Intent question for Duong becomes an FYI in the summary, not a gate.

### (c) CvJdAPI `AI_SERVICE_URL` → unpublished port
`appsettings.Development.json:8` = `http://localhost:8000`; compose publishes `8080:8000` (`docker-compose.yml:19`). Broken whenever ai-service is containerized. Four hardcoded fallbacks at `CvEndpoints.cs:48`, `JdEndpoints.cs:38`, `:96`, `MatchEndpoints.cs:30`. **TIER 1** (config/compose DNS) with a courtesy note.

---

## Phase 1 — Full-stack containerization

**Closes:** criterion 7 (0→3), F-11, F-06, F-12, criterion 8 (1→2), criterion 3 (1→2). **Effort: L → M–L** (see re-sequencing).

### 1.1 Blocking prerequisite — **the only part of Phase 1 still TIER 3**

No schema exists in the repo. Verified: `grep -rl "CREATE TABLE" --include="*.sql"` → zero results. Four incremental scripts on disk; **only `PaymentAPI/Sql/001_…sql` is tracked** — the rest excluded by `.gitignore:106`.

1. Generate per-database baselines by hand via **SSMS → Generate Scripts** → commit as `db/baseline/000_<Database>_baseline.sql`. Hand-authored, not EF-generated — constraint honoured. **TIER 3.**
2. **Remove `.gitignore:106`** so `source/*/Sql/*.sql` is tracked. **TIER 3.**
3. Make every script idempotent (`IF NOT EXISTS`); `002_…sql` shows the pattern.

**This is the sequencing risk in the whole plan.** Everything else in Phase 1 is TIER 1 and can start immediately, but the stack cannot actually *boot* until the schema question clears the team. Start the TIER 1 work now; raise §1.1 at the next standup in parallel.

### 1.2 Per-service .NET container spec — **TIER 1 throughout**

Pinned images (verify current patch on MCR): build `mcr.microsoft.com/dotnet/sdk:8.0.404-noble`, runtime `mcr.microsoft.com/dotnet/aspnet:8.0.11-noble`. Chiseled variants are non-root and smaller but ship no shell for `HEALTHCHECK` — use plain `-noble` + explicit non-root user; chiseled is post-thesis hardening.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src
COPY DevPrep.sln ./
COPY InterviewAPI/InterviewAPI.csproj InterviewAPI/
COPY ../contracts/auth/auth.csproj ../contracts/auth/
RUN dotnet restore InterviewAPI/InterviewAPI.csproj
COPY . .
RUN dotnet publish InterviewAPI/InterviewAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-noble AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appgroup && useradd -r -g appgroup -s /sbin/nologin appuser
WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 DOTNET_EnableDiagnostics=0
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "InterviewAPI.dll"]
```

Uniform in-container port **8080**. `.dockerignore` per project:
```
bin/
obj/
.vs/
.git/
.github/
**/node_modules/
**/*.user
**/appsettings.*.local.json
.env
.env.example
**/*.md
Dockerfile
.dockerignore
```
Excluding `.env*` is deliberate — half the F-12 fix.

### 1.3 SQL Server container — **TIER 1** (the password change is explicitly TIER 1)

| Item | Spec |
|---|---|
| Image | `mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04` (pin; verify CU) |
| User | Non-root `mssql` (UID 10001) by default |
| Env | `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}`, `MSSQL_PID=Developer` |
| Volume | `mssql-data:/var/opt/mssql` |
| Healthcheck | `sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"` — `interval=10s retries=10 start_period=30s` |

**Forcing function:** the current password `12345` (`.env:1-11`) **will not start a SQL Server container** — the image enforces ≥8 chars with complexity and exits. Containerization mandates the fix, which is also criterion 8.

**Init:** one-shot `db-init` service (`mcr.microsoft.com/mssql-tools`) depending on `sqlserver: service_healthy`, running `db/init.sh` — create the catalogs, apply baselines, then `source/*/Sql/*.sql` in lexical order, all idempotent. Application services declare `depends_on: db-init: { condition: service_completed_successfully }`; `service_healthy` alone would race schema creation.

Connection strings already read from `<X>_DB_CONNECTION_STRING` in all 11 `Program.cs` — no code change, only the value moves to `Server=sqlserver,1433;…`.

### 1.4 `localhost` → compose DNS — **all TIER 1**

| # | File:line | Current | Becomes |
|---|---|---|---|
| 1–10 | `ApiGateway/appsettings.json:343,350,360,367,374,381,388,395,402,409` | `http://localhost:7001–7010/` | `http://{authapi,userapi,cvjdapi,questionapi,interviewapi,evaluationapi,feedbackapi,paymentapi,newsapi,notificationapi}:8080/` |
| 11 | `ApiGateway/Program.cs:36` | CORS default `http://localhost:5130` | `${CORS_ALLOWED_ORIGINS}` |
| 12–14 | `UserAPI/Program.cs:49,54,73` | `new Uri("http://localhost:{7001,7005,7008}/")` **C# literals** | config-read env vars *(logged as courtesy — §0.2)* |
| 15 | `WebClient/appsettings.json:4` | `http://localhost:7000/` | `http://apigateway:8080/` |
| 16–17 | `InterviewAPI/appsettings.json:14`, `Program.cs:56` | `localhost:8080` / fallback `:8000` | `http://ai-service:8000`, no literal fallback |
| 18–19 | `EvaluationAPI/appsettings.json:14`, `Program.cs:40` | same | same |
| 20–21 | `QuestionAPI/appsettings.json:10`, `Program.cs:87` | same | same |
| 22–26 | `CvJdAPI/appsettings.Development.json:8`; `CvEndpoints.cs:48`; `JdEndpoints.cs:38`, `:96`; `MatchEndpoints.cs:30` | `localhost:8000` (**broken today**) | `http://ai-service:8000`, single config read |
| 27 | `.env:63` | `VNPay__ReturnUrl=http://localhost:7008/vnpay/return` | **stays a host URL — do NOT convert** |
| 28 | `.env:1-11` (×11) | `Server=localhost\MSSQLSERVER01` | `Server=sqlserver,1433` |
| 29 | `.env:7` | `STATISTICS_DB_CONNECTION_STRING` | **delete** — service removed by PR #78 |

Row 27 is the classic trap: VNPay redirects the **user's browser**, not a container.

### 1.5 Mandatory port lockdown `[CONTRADICTED A-10 → hard gate]`

Exactly **two** services publish to the host: `apigateway` (`7000:8080`) and `webclient` (`5130:8080`). Every other service — including `sqlserver` and `rabbitmq` — has **no `ports:` block**. A `ports:` entry elsewhere is a review-blocking defect: centralizing auth at the gateway while service ports stay reachable would make the system *less* secure than today.

**Bounded dev exception:** `sqlserver:1433` and `rabbitmq:15672` may be published in `docker-compose.override.yml` only, never the base file.

### 1.6 Order of operations — both run modes stay working

| Step | Action | Local dev after |
|---|---|---|
| 1 | Commit baseline schema; un-ignore `source/*/Sql/` **(TIER 3 — gated)** | Unaffected |
| 2 | Every literal (§1.4) → **env var + current localhost value as default** | Works — defaults reproduce today exactly |
| 3 | Per-project `Dockerfile` + `.dockerignore` | Works — not read by `dotnet run` |
| 4 | Add `sqlserver` + `db-init`; local `.env` → `Server=localhost,1433` | Works via the dev-override port |
| 5 | Add .NET services to compose one at a time | Works in either mode |
| 6 | Remove `.env.example` load from all 11 `Program.cs` (F-12) | Works |
| 7 | Delete orphaned `source/Dockerfile` (F-11) | Works |

Step 2 is the linchpin: every literal becomes *env-var-with-current-default*, so container and local mode diverge only by environment, never by code. Also the permanent F-06 fix. **Given Caveat 1, pull `dev` immediately before step 2 and commit per-service, not in one batch.**

### Acceptance criteria (<2 min)
1. Clean machine: `git clone && cd infra && docker compose up -d`.
2. `docker compose ps` → 15 services healthy, `db-init` `exited (0)`.
3. Browser → `localhost:5130` → log in → start interview → answer a question.
4. `docker compose exec apigateway curl -fsS http://interviewapi:8080/health` → 200.
5. **Lockdown gate:** `curl http://localhost:7001..7010/health` from the host → **all refused**. Any success fails the phase.

**Risk:** baseline schema not matching dev databases — no authoritative source to diff against. Mitigation: generate from the most-current machine; each service smoke-tested against a fresh volume.
**Rollback:** revert the compose commit; step 2 preserves defaults so app code keeps working.

---

## Phase 2 — Observability baseline

**Closes:** criterion 9 (0→2), F-14. **Effort: M.**

| Action | Path | Tier |
|---|---|---|
| MODIFY | `ApiGateway/Program.cs` — generate/forward `X-Correlation-Id` | TIER 1-adjacent *(gateway, additive)* |
| CREATE | `{Interview,Evaluation}API/Middleware/CorrelationIdMiddleware.cs` | Mine |
| CREATE | `{Interview,Evaluation}API/Http/CorrelationForwardingHandler.cs` — one `DelegatingHandler`, not per-client edits | Mine |
| MODIFY | `ai-service/app/main.py` — accept/echo/log correlation id | Mine |
| MODIFY | 7 teammate `Program.cs` — correlation middleware + real readiness check (F-14) | **TIER 2 (unlisted — flagged §0.1)** |
| MODIFY | `*.csproj` — `Serilog.AspNetCore` + JSON console sink | **TIER 3-adjacent** — new library, needs team nod |

Serilog is a new dependency. `CLAUDE.md` names SignalR/MediatR/AutoMapper/FluentValidation as forbidden; a logging sink is a different category but still worth a team nod. **Fallback needing no agreement:** `ILogger` scopes + built-in `AddJsonConsole()` — zero new packages, ~80% of the value. Recommend starting with the fallback so Phase 2 is fully unblocked.

**Acceptance:** `curl -H "X-Correlation-Id: demo-123"` → `docker compose logs | grep demo-123` shows it in ApiGateway, InterviewAPI, EvaluationAPI, ai-service. Stop SQL Server → `/health` → 503 within 30 s.

---

## Phase 3 — Trust consolidation + F-17 closure

**Closes:** criterion 11 (0→3), criterion 5 (1→3), **F-17 (High)**, F-02, part of F-03, F-13.
**Effort: M → S–M for the unblocked part** (see re-sequencing).

### 3.1 F-17 — ai-service publicly exposed via cloudflared with most routes unauthenticated

**Severity: High.** A cloudflared Quick Tunnel is run manually to give GPU-less teammates access to ai-service (host `8080`, `docker-compose.yml:19`). Verified router-level auth:

| Routes | Source | Auth |
|---|---|---|
| `/parse/cv`, `/parse/jd`, `/parse/jd-file` | `app/cv/router.py:13`, `app/jd/router.py:13` | **NONE** |
| `/match` | `app/match/router.py:6` | **NONE** |
| `/interview/*` (9 endpoints incl. `/tts`) | `app/api/interview.py:28` | **NONE** |
| `/evaluate` | `app/api/evaluation.py:13` | **NONE** |
| `/catalog` | `app/api/catalog.py:12` | **NONE** |
| `/ui` static test UI | `app/main.py:71` | **NONE** |
| `/internal/*` | `app/api/internal.py:15` + `_check_key` | Key — **fails open on empty key** (`:21-24`) |
| `/internal/biometrics/*` | `biometrics/router.py:36-37` `Depends(require_internal_api_key)` | Key (same fail-open caveat) |

CORS is `*` (`.env:40` → `main.py:45-51`).

**Why more serious than F-05:** F-05 is a worse defect but local-only. F-17 is genuine public exposure of (1) **unauthenticated PII ingestion** — `/parse/cv` accepts arbitrary PDF/DOC uploads (`cv/router.py:25-29`) onto the GPU; (2) **unmetered GPU compute** across ~15 routes on a `--workers 1` process, so a few concurrent requests starve biometrics and grading for real users; (3) **latent escalation** — if `INTERNAL_API_KEY` is ever unset, the fail-open branches expose embedding endpoints and the RAG index at the same URL.

**Mitigating factors, weighed:** the tunnel is undocumented in the repo (only trace: `.gitignore:77`, `infra/cloudflared.deb`; no compose service, no config, nothing in `run-all.ps1`), started by hand, so exposure is **intermittent**, and Quick Tunnel URLs are random and ephemeral. That is why this is High, not Critical — **and also why it is easy to under-rate: it leaves no repo artifact, so no code review or CI will ever surface it.**

**Immediate operational mitigation — no code, no coordination, do today:** stop tunneling `ai-service:8080` directly. Either tunnel `apigateway:7000` so traffic traverses gateway controls, or put Cloudflare Access in front, or tear the tunnel down when idle. Any one removes the public unauthenticated surface. Do this independent of phase scheduling.

### ► **Can Phase 3 now proceed end-to-end? Yes — this is the single biggest unblock in the revision.**

Previously this required a **three-step coordinated landing with Duong**: (1) he adds `X-Internal-Api-Key` to CvJdAPI's `"ai"` HttpClient, (2) I add the router dependency to ai-service, (3) joint verification. Step 1 gated steps 2–3 on someone else's availability.

Under TIER 2, **the CvJdAPI header is mine to add**. So it collapses to one atomic change I control end-to-end:

| Action | Path | Tier |
|---|---|---|
| MODIFY | `CvJdAPI/Program.cs:22-25` — `"ai"` HttpClient sends `X-Internal-Api-Key` | **TIER 2** (explicitly listed) |
| MODIFY | `ai-service/app/cv/router.py:13`, `jd/router.py:13`, `match/router.py:6` — add `Depends(require_internal_api_key)` | Mine |
| MODIFY | `ai-service/app/api/{interview,evaluation,catalog}.py` — same, **after verifying no external consumer** | Mine |
| MODIFY | `ai-service/app/api/internal.py:21-24` + biometrics key check — fail closed on empty key | Mine |
| MODIFY | `ai-service/app/core/config.py` — `ALLOWED_ORIGINS` must not default to `*` | Mine |
| MODIFY | `ai-service/app/main.py:67-71` — gate `/ui` behind `ENABLE_TEST_UI` | Mine |
| CREATE | `docs/architecture/ai-service-exposure.md` — document the tunnel and the rule that it fronts the gateway | Mine |

**Ordering within the commit still matters** even though one person makes it: land the CvJdAPI header first (ai-service ignores an unexpected header while still unauthenticated), then the ai-service dependency. Verify `/parse/cv`, `/parse/jd`, `/match` before pushing. **Notify Duong after** with the §0.2 entries — he owns the consumer even though he no longer gates the change.

`/interview/*`, `/evaluate`, `/catalog` have no external consumer (the .NET flow uses `/internal/*` exclusively per `CLAUDE.md`) — verify, then protect unilaterally.

### 3.2 Internal-auth tier consolidation `[CONFIRMED A-5]`

Two tiers by design: **`X-Internal-Api-Key` = service-to-service; JWT+role = human via WebClient.** The defect is the blurred boundary, not the two tiers — so AuthAPI's `InternalAccountsController` (`:24-25`) is **correct as tier 2** and needs no change.

| Action | Path | Tier |
|---|---|---|
| MODIFY | `QuestionAPI/Controllers/InternalQuestionController.cs:40-45` — remove fail-open | **TIER 2 (unlisted — flagged)** |
| MODIFY | `QuestionAPI/Program.cs` — refuse startup without `INTERNAL_API_KEY`, per `NotificationAPI/Program.cs:47-56` | **TIER 2 (unlisted)** |
| MODIFY | `ApiGateway/Program.cs` — JWT validation at the edge, **additive** | **TIER 3 — still gated** |
| DELETE | `AddCors`/`UseCors` in 7 services (F-13) | TIER 1-adjacent |
| CREATE | `docs/architecture/internal-auth-convention.md` | Mine |

**Phase 3 now splits:** §3.1 + the F-02 items are fully unblocked; only gateway JWT centralization remains TIER 3.

### Acceptance
1. Unset `INTERNAL_API_KEY` for QuestionAPI → container **fails to start**.
2. **F-17 gate:** from outside the compose network, `/parse/cv` → 401, `/interview/start` → 401, `/health` → 200.
3. `/parse/cv`, `/parse/jd`, `/match` still work **through CvJdAPI** (Duong's regression gate).
4. *(TIER 3, when approved)* gateway call without a token → 401 at the edge, request absent from InterviewAPI logs.
5. Lockdown gate from Phase 1 still refused.

**Risk:** gateway auth can break legitimately anonymous routes — explicit allowlist (`/api/auth/login`, `/api/auth/register`, `/api/news/*`) enumerated and tested first. Second risk: landing the ai-service dependency before the CvJdAPI header breaks CV/JD — hence the intra-commit ordering above.

---

## Phase 4 — Async messaging reference implementation

**Closes:** criterion 4 (1→2), criterion 6 (1→2), F-07, part of F-08. **Effort: M.** **Partly TIER 3 — RabbitMQ needs team agreement.**

Flow: **InterviewAPI → EvaluationAPI grading trigger** — both mine, replaces F-08's worst 4-hop chain.

```csharp
namespace DevPrep.Contracts.Interview;

public record AnswerSubmittedForGrading
{
    public Guid   SessionId         { get; init; }
    public Guid   SessionQuestionId { get; init; }
    public Guid   UserId            { get; init; }
    public string QuestionText      { get; init; } = null!;
    public string ReferenceAnswer   { get; init; } = null!;
    public string UserAnswer        { get; init; } = null!;
    public Guid   AttemptId         { get; init; }   // idempotency key
    public DateTime OccurredAt      { get; init; }   // VietnamTime.Now
}
```

**Contract library:** `contracts/interview-events/InterviewEvents.csproj`, mirroring the proven `contracts/auth/auth.csproj` pattern. **Outbox:** `OutboxMessages` table in `DevPrep_Interview` — same DB, so the insert is atomic with the `ChatMessages` write in one `SaveChanges`. No distributed transaction, no lost event. Publisher is a `BackgroundService` modelled on the existing `GradingBackgroundService.cs`. Consumer is idempotent on `(SessionQuestionId, AttemptId)` `[ASSUMED A-6]`.

**Degradation when the broker is down** — the question a board will ask: outbox rows accumulate durably (zero loss, request never fails); the existing in-process `GradingBackgroundService` stays behind a `Grading:UseBroker` flag so grading continues; on broker return the publisher drains in order. Keeping both paths makes the phase reversible *and* gives a live demo of graceful degradation instead of a claim about it.

**Sequencing note:** the outbox table, contract library, publisher, and consumer can all be built and unit-tested while the RabbitMQ decision is pending — only the compose service and the MassTransit registration are gated. Build first, wire last.

**Acceptance:** publish+consume visible in the management UI; `docker compose stop rabbitmq` → answer still returns 200 and outbox count increments; restart → backlog drains with no duplicate `AnswerEvaluations` row.

---

## Phase 5 — Resilience and cross-service consistency

**Closes:** criterion 10 (1→3), F-09/F-10, rest of F-08. **Effort: S–M** (A-7b).

### 5.1 Justification — production-intent framing (use this wording)

> VNPay points at the sandbox endpoint (`.env:62`) and no live credentials exist. That is exactly why the failure mode must be fixed now: the refund path today logs and forgets (`PaymentApiClient.cs:104-112`), so a failed refund is silently lost with no automated recovery. A silent-loss failure mode on a credit ledger is not something to ship and then switch to live payments behind — correctness has to exist *before* the sandbox-to-live switch. We are fixing it while it is cheap and the blast radius is zero.

**Never claim live user funds are at risk** — a reviewer can open `.env` in ten seconds.

| Action | Path | Tier |
|---|---|---|
| MODIFY | `InterviewAPI/Program.cs:56-104`, `EvaluationAPI/Program.cs:43-60` — Polly circuit breaker + retry with jitter | Mine |
| MODIFY | `InterviewAPI/Clients/PaymentApiClient.cs:164-187` — `GetBalanceAsync` must not return `0` on failure | Mine |
| CREATE | `PaymentAPI/Sql/003_credit_ledger_reference_unique.sql` — unique `(ReferenceId, EntryType)` `[ASSUMED A-6]` | **TIER 2 (unlisted — flagged)** |
| MODIFY | `PaymentAPI/Services/InternalCreditService.cs` — return existing row on duplicate | **TIER 2 (unlisted)** |
| MODIFY | `InterviewAPI/Services/Outbox/` — failed refunds become durable outbox rows `[CONFIRMED A-1]` | Mine |
| CREATE | orphan-account sweep `IHostedService` in AuthAPI or UserAPI `[ASSUMED A-8]` | **TIER 2 (unlisted)** |

**Sweep simplification `[A-7b]`:** `UserAPI.CreateProfileIfNotExistsAsync` (`Services/UserProfileService.cs:38`) is **idempotent by construction** — verified by the grep you requested, which found it and zero `orphan`/`reconcil` matches. The sweep only needs to **find** orphans and re-invoke; no de-duplication, no bookkeeping, and re-invoking on a non-orphan is a safe no-op.

`PaymentApiClient.GetBalanceAsync` returning `0` on any failure (`:175`) is the most dangerous line in my own code — a PaymentAPI outage is indistinguishable from an empty wallet, so users see "insufficient credits" instead of "service unavailable."

**Acceptance:** stop PaymentAPI → "Credit service unavailable," not "insufficient credits"; same deduct twice with one `ReferenceId` → one ledger row; stop PaymentAPI mid-session → refund queued → restart → completes.

---

## Phase 6 — Restore data ownership (cross-database SQL removal)

**Closes:** criterion 2 (1→3), criterion 1 (2→3), F-01 (Critical). **Effort: M–L → M.** **Now entirely TIER 2 — no longer blocked on anyone.**

| Action | Path |
|---|---|
| MODIFY | `PaymentAPI/Program.cs:456-460`, `ManagerPaymentsController.cs:210-215` — joins → batch internal calls |
| MODIFY | `UserAPI/Controllers/ManagerUsersController.cs:59-92,171-175,213-229,255-266` — same |
| CREATE | `InterviewAPI/Controllers/InternalSessionStatsController.cs` — batch endpoint UserAPI needs *(mine)* |
| CREATE | PaymentAPI + AuthAPI batch internal endpoints |

`[ASSUMED A-4]` all new endpoints are **batch** (`POST` + id list → keyed result), mirroring `AccountLookupRequest`/`AccountLookupItem` in `AuthAPI/DTOs`. Per-row endpoints would turn one SQL query into N HTTP calls on an admin list page.

**Acceptance:** `grep -rE "DevPrep_\w+\.dbo\." source/ --include=*.cs` → **zero results**; admin user-list renders equivalent data; page load within ~2× baseline.
**Risk — unchanged and now more relevant:** this is the highest-regression phase, and being able to make the changes *unilaterally* removes the review-by-owner that previously acted as a safety net. Compensate: schedule after Phase 2 so observability exists, do one query at a time, capture before/after screenshots, and send the §0.2 summary promptly so owners can sanity-check the semantics.

---

## Phase 7 — F-16: ai-service bundles four capabilities in one process

`ai-service` is one FastAPI process (`main.py:43`) mounting eight routers across four capabilities consumed by four .NET services, all loading in one lifespan (`main.py:29-40`) under `--workers 1` (`Dockerfile:77`).

**Real costs, stated before the board can:** no bulkhead (a 5-minute CV parse can stall grading — the same weakness F-17 lets an anonymous caller trigger); single point of failure for four consumers; deploy coupling with Duong; `--reload` shipped in the production `CMD`.

**Recommendation: keep merged this cycle.**

> ai-service is not a business bounded context — it is a **shared capability provider**, architecturally equivalent to a database or cache. Microservice decomposition applies to business capabilities needing independent evolution; the four *business* contexts are already separate .NET services owning their own data. ai-service is their shared implementation detail behind a stable internal contract.
>
> Splitting it would be actively harmful here. All four capabilities share one GPU, one CUDA context, one set of memory-mapped weights. Four processes would multiply resident VRAM beyond the demo hardware or force CPU fallback, eliminating the acceleration the system is built on. The gain is theoretical isolation; the cost is a system that no longer runs.

| Action | Path |
|---|---|
| MODIFY | `ai-service/Dockerfile:77` — remove `--reload` from production `CMD` |
| MODIFY | `ai-service/Dockerfile:11` — pin `nvidia/cuda:12.1.1-runtime-ubuntu22.04` by digest |
| MODIFY | `ai-service/Dockerfile` — non-root user + `HEALTHCHECK` (currently neither) |
| CREATE | `ai-service/app/core/concurrency.py` — per-capability `asyncio.Semaphore`; CV/JD capped so it cannot starve interview/biometrics |
| MODIFY | `ai-service/Dockerfile:58-66` — stop baking the Piper voice; mount via volume |
| CREATE | `docs/architecture/ai-service-split-criteria.md` — triggers: second GPU, CV/JD >20% GPU time, or Duong needing independent release cadence. Biometrics splits first |

All mine. **Duong notification is now post-hoc**, but the regression gate stands: `/parse/cv`, `/parse/jd`, `/match` unchanged, including the `X-Internal-Api-Key` header from Phase 3.

---

## Phase 8 — API contracts, versioning, StatisticsAPI cleanup

**Closes:** criterion 12 (1→2), F-04. **Effort: S → XS.**

**Revised by the freeze check:** StatisticsAPI is **already removed from `dev`** (PR #78, 4 days ago — `git ls-tree -r dev | grep -i statisticsapi` is empty). What remains is untracked `bin/`/`obj/` residue on local disks. So:

| Action | Path | Tier |
|---|---|---|
| CREATE | `docs/architecture/api-versioning-policy.md` — additive-only internally; `/v2/` for breaking changes | Mine |
| MODIFY | `contracts/README.md` — extend beyond the 3 JSON schemas to REST endpoints; remove Statistics (`:31`) | Mine |
| MODIFY | `docs/architecture/pure-microservices-baseline.md:39` — remove the `/api/statistics/*` row | Mine |
| DELETE | `source/StatisticsAPI/` (local, untracked residue) | TIER 1 |
| DELETE | `.env:7` `STATISTICS_DB_CONNECTION_STRING` | TIER 1 |

F-04 is now purely documentary — no gateway route to add, no service to remove.

---

## Re-sequencing summary — what got faster, and by how much

| Phase | Before | After | Delta |
|---|---|---|---|
| **1 Containerization** | L; blocked on UserAPI/QuestionAPI/CvJdAPI owners for 15 of 29 config rows | **M–L**; all 29 rows TIER 1 and mine. **Only §1.1 (schema/`.gitignore:106`) stays TIER 3** | Body unblocked entirely; the *boot* still waits on one team decision |
| **2 Observability** | M; teammate `Program.cs` edits needed hand-off | **M**, unblocked — if the built-in `AddJsonConsole()` fallback is used instead of Serilog | Removes one hand-off and one library approval |
| **3 Trust + F-17** | M; **three-step coordinated landing with Duong** for `/parse/*` | **S–M for §3.1 + F-02, fully end-to-end and atomic.** Gateway JWT stays TIER 3 | **Biggest unblock.** A multi-day round-trip becomes a single commit |
| **4 Async messaging** | M; RabbitMQ gated | **M**, unchanged — outbox/contracts/consumer buildable now, broker wiring gated | No speedup, but ~70% can start immediately |
| **5 Resilience** | S–M; PaymentAPI schema needed sign-off | **S–M**, unblocked as TIER 2 *(pending your confirmation of the unlisted items)* | Removes a schema-approval round-trip |
| **6 Data ownership** | M–L; **entirely blocked on two owners**, highest coordination cost | **M**, entirely TIER 2 and mine | Largest scope-of-work unblock; loses owner review as a safety net |
| **7 ai-service** | M; Duong pre-approval on shared routes | **M**, notify-after | Removes a blocking review |
| **8 Contracts/Statistics** | S | **XS** — service already deleted upstream | Shrinks to doc edits + local cleanup |

**Net:** four phases lose a blocking hand-off (3, 5, 6, 7); one shrinks (8); **three TIER 3 items remain the only true gates** — schema commit + `.gitignore:106`, gateway JWT centralization, RabbitMQ. All three are worth raising at a single standup rather than three separate conversations.

**Critical-path observation:** Phase 1's TIER 3 prerequisite (§1.1) is now the single largest scheduling risk in the plan. Everything else in Phase 1 can be built today, but nothing boots without schema. **Raise §1.1 first, before starting the TIER 1 work**, so the approval runs in parallel rather than after.

---

## Board-defence mapping

| Likely question | Phase | What to show |
|---|---|---|
| "Is this deployable as microservices, or projects in one solution?" | 1 | Clean machine `docker compose up -d`; `docker compose ps` → 15 healthy, no SQL Server on host |
| "Show me database-per-service is real" | 6 | `grep -rE "DevPrep_\w+\.dbo\." source/` → zero, contrasted with the audit's before-state |
| "Are services isolated, or just on different ports?" | 1, 3 | `curl localhost:7005/health` → refused; only 7000 and 5130 answer |
| "What happens when one service goes down?" | 5 | `docker compose stop paymentapi` live → "service unavailable," system keeps working |
| "Where is your async communication?" | 4 | RabbitMQ UI publish+consume during a live answer; then stop the broker and show the request still succeeding |
| "How do you trace a request across services?" | 2 | One correlation id → `docker compose logs \| grep` across 4 services |
| "How do services authenticate to each other?" | 3 | Unset `INTERNAL_API_KEY` → refuses to start; external `/parse/cv` → 401 |
| "Why is ai-service one process doing four things?" | 7 | Written justification + concurrent CV parse and interview answer not blocking |
| "Policy on breaking API changes?" | 8 | `api-versioning-policy.md` |
| "Do you understand your own weaknesses?" | the audit | 11/36 self-identified with evidence, plus F-17 found by follow-up. We found these before you did |

---

## Projected scoring

| Criterion | Now | Target | Driver |
|---|---|---|---|
| 1 Decomposition | 2 | **3** | Phase 6 + StatisticsAPI cleanup |
| 2 Database-per-service | 1 | **3** | Phase 6 |
| 3 Independent deployability | 1 | **2** | Phase 1 (capped — no per-service CI/CD) |
| 4 Inter-service communication | 1 | **2** | Phase 4 (one async flow) |
| 5 Gateway & exposure | 1 | **3** | Phase 1 + 3 incl. mandatory lockdown |
| 6 Data consistency | 1 | **2** | Phase 4 + 5 |
| 7 Containerization | 0 | **3** | Phase 1 |
| 8 Config & secrets | 1 | **2** | Phase 1 (capped — no vault) |
| 9 Observability | 0 | **2** | Phase 2 (capped — no tracing backend) |
| 10 Resilience | 1 | **3** | Phase 5 |
| 11 Service-to-service security | 0 | **3** | Phase 3 + §0 + F-17 |
| 12 API contracts | 1 | **2** | Phase 8 |

**Total: 11/36 → 30/36 (≈83%).** All three criteria currently at 0 reach 2 or 3.

**If time collapses** (contingency only — the plan is shaped for full scope): cut Phase 8 first (−1 → 29), then Phase 6 (−3 → 26; reluctantly — it answers the board's central criticism), then Phase 4 (−2 → 24). Phases 1, 2, 3, 5 alone still yield **24/36** and close every Critical finding. **Never cut Phase 1.**
