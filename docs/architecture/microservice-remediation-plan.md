# DevPrep Microservice Remediation Plan

**Companion to:** `docs/architecture/microservice-audit.md` (current score **11/36**). Finding IDs (F-01 … F-17) and criterion numbers refer to that document, except **F-17**, which is new and defined here.
**Status:** planning only. No source file was modified, no build or service start was performed while producing this plan. This file is the only artifact written.
**Target:** full scope per Section F Q16 — *microservice-correct where the board can verify it*: full containerization, database-per-service, one real async flow, basic observability, Critical security findings closed. Explicitly **not** in scope: per-service CI/CD, repository splitting.
**Projected score on completion: ~30/36.**

---

## 0.0 — Assumption register (reconciled)

Section F has now been answered. This register records the final state of each assumption made in the first draft.

### Confirmed

| # | Question | Final answer | Effect on plan |
|---|---|---|---|
| A-1 | Q1 — refund failure tolerance | **Automated recovery required.** Manual reconciliation not acceptable to ship | Phase 5 keeps outbox + retry. **Justification rewritten** — see §5.1 |
| A-3 | Q3 — is F-05 live | **Local-only.** The cloudflared tunnel fronts ai-service:8080, not ApiGateway:7000 | §0(a) urgency stays *urgent-but-not-incident*. Consequence: **new finding F-17** |
| A-5 | Q6 — internal-auth tiers | **Exactly two tiers.** `X-Internal-Api-Key` = service-to-service; JWT+role = human via WebClient. The defect is the blurred boundary, not the existence of two tiers | Phase 3 unchanged in shape; AuthAPI's `InternalAccountsController` is *correct as-is* (tier 2), not a defect |
| A-7 | Q8 — does a reconciliation job exist | **No.** Verified read-only: `grep -rn "CreateProfileIfNotExists\|orphan\|reconcil" source/` returns only UserAPI's own handler (`Program.cs:176`, `Services/UserProfileService.cs:38`) — zero `orphan`/`reconcil` matches | Phase 5 sweep confirmed needed, and **simplified** — see A-7b |
| **A-7b** | *(new, from the same grep)* | `CreateProfileIfNotExistsAsync` is **idempotent by construction** | Sweep only needs to *find* orphans and re-invoke; no de-duplication logic required. Phase 5 drops from M to **S–M** |
| A-9 | Q11 — who approves JWT centralization | **Full-team sign-off.** I draft the proposal, team decides | Phase 3 gated on a team decision, not planned as unilateral |
| A-11 | Q13 — StatisticsAPI | **Out of scope.** Remove stub, update docs | Phase 8. **Refined** — see A-11b |
| **A-11b** | *(new, from verification)* | `source/StatisticsAPI/` is **not in `DevPrep.sln`**, has **no `.csproj`**, and is **entirely untracked** (`git ls-files` empty; only `bin/`/`obj/` artifacts) | Deletion is a **local filesystem cleanup with zero git diff and zero build risk**. The only committed part of F-04 is the doc edits |
| A-12 | Q15 — DotNetEnv precedence | **Do not investigate.** Fix is identical either way | Phase 1 step 6 removes the `.env.example` load; precedence never needs resolving |

### Contradicted

| # | Original assumption | Corrected answer | What changed |
|---|---|---|---|
| **A-10** | Q12 — gateway auth is additive; port lockdown a *free side effect* of good compose topology | **Port lockdown is MANDATORY and non-negotiable.** Only `apigateway` and `webclient` publish ports; every other service is internal-network-only with no `ports:` block. Centralizing auth while leaving ports open would make security *worse* than today | Lockdown is promoted from an incidental property to an **explicit acceptance gate** in both Phase 1 and Phase 3, with a negative test. Criterion 5 and 11 now reach **3**, not 2 |

**Note on A-3:** my first draft assumed local-only; the answer initially reported "LIVE"; verification of the tunnel target restored the original assumption. A-3 therefore stands **as originally written**, but the round-trip produced F-17, which is the more serious issue.

### Still open — tagged, not guessed

| # | Question | Why it stays open |
|---|---|---|
| **A-2** | Q2 — identity model for `/credits/balance`: JWT-everywhere vs. a trusted-internal-caller path | Unanswered. Plan proceeds on *split routes*; §0(a) states what changes if the other option is chosen |
| **A-4** | Q5 — batch vs. per-row internal read APIs replacing F-01 | Unanswered. Plan proceeds on *batch*; Phase 6 drops from M–L to S–M if per-row is acceptable |
| **A-6** | Q7 — credit idempotency: DB constraint vs. app-code check | Unanswered. Plan proceeds on *DB-level unique index*; Phase 5 drops to S and needs no PaymentAPI schema sign-off if app-level suffices |
| **A-8** | Q9 — orphan sweep data access: internal APIs vs. direct DB read | Unanswered. Plan proceeds on *internal APIs*, consistent with F-01's own remediation |

### Standing constraint on board-facing claims

**Never assert that live user money flows through the system.** `.env:62` sets `VNPay__PaymentUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` — the sandbox endpoint — and no live VNPay credentials exist anywhere in the repo. A reviewer can open `.env` and check in ten seconds. Every justification for the credit-consistency work is framed as **production-intent**: the design must be correct *before* the sandbox-to-live switch, because a silent-loss failure mode is unacceptable to ship. That argument is both true and stronger.

### Standing tension: manual migrations vs. clean-machine bring-up

Migrations remain **hand-authored `.sql`, never EF-generated** (constraint honoured), but they are committed to the repo and executed by an init container. If the intent was "a human runs them in SSMS every time," clean-machine `docker compose up` is unachievable and Phase 1's acceptance criteria need renegotiating.

---

## 0 — Out-of-band items (not scheduled into any phase)

Teammate-owned code. Listed outside the phase plan because they are security defects or a broken flow that should not wait for a phase boundary.

### (a) F-05 — Unauthenticated `X-User-Id` trust on PaymentAPI `/credits/balance` — **CRITICAL severity, local-only reachability**

**Evidence:** `source/PaymentAPI/Program.cs:235-251` reads identity from a client-supplied header with no `[Authorize]`, no JWT check, no internal-key check. PaymentAPI's internal-key middleware only guards `/api/internal` (`Program.cs:151`); this route doesn't match. Gateway-reachable in principle via `ApiGateway/appsettings.json:86-96`.

**Reachability — corrected `[CONFIRMED A-3]`:** the cloudflared Quick Tunnel fronts **ai-service:8080 only**, not ApiGateway:7000. F-05 is therefore reachable **only from the local machine / LAN**, not the public internet. Severity remains Critical — an authentication bypass on a financial endpoint is intrinsically severe — but **urgency is "next standup," not "same day."** Fix before any public gateway exposure ever happens.

**Minimal correct fix** (PaymentAPI owner, ~30 min) `[ASSUMED A-2]`:
- `GET /credits/balance` → add `[Authorize]`; derive `userId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)`; ignore `X-User-Id`.
- Add `/api/internal/credit/balance` for InterviewAPI — inherits the existing fail-closed middleware (`Program.cs:139-169`).
- `source/InterviewAPI/Clients/PaymentApiClient.cs:164-181` switches to the internal route (MINE — same-day once the endpoint exists).

**If A-2 resolves the other way** (JWT-everywhere): PaymentAPI keeps one route with `[Authorize]`, and InterviewAPI must forward the end user's bearer token via `IHttpContextAccessor` (already registered, `InterviewAPI/Program.cs:107`). Larger InterviewAPI diff, smaller PaymentAPI diff.

**Interim gateway mitigation (additive, mine):** reject `/api/payment/credits/balance` at the gateway when no `Authorization` header is present, alongside the existing internal-path blocklist (`ApiGateway/Program.cs:68-83`). **Honest limitation:** blocks anonymous callers only; any logged-in user could still spoof another `X-User-Id` until the real fix lands.

### (b) F-15 — CvJdAPI has no authentication on any endpoint

**Evidence:** `source/CvJdAPI/Program.cs` (73 lines) — no `AddAuthentication`/`AddAuthorization`; endpoints mapped at `:61-63` with no `RequireAuthorization()`. Gateway-reachable via `/api/document/{**catch-all}`. AI HttpClient carries a 5-minute timeout (`Program.cs:22-25`).

**Intent unknown** — stays in §0 regardless of whether it turns out to be deliberate (stub) or an oversight. Ask Duong; the answer changes the label, not the presence of the item.

**Minimal correct fix** (Duong, ~1 h): copy the JWT block from `InterviewAPI/Program.cs:115-118`, add `UseAuthentication`/`UseAuthorization`, `.RequireAuthorization()` on the three endpoint groups.

**Interim gateway mitigation (additive, mine):** require a present `Authorization` header for `/api/document/*`. Note the gateway's 30 s timeout (`ApiGateway/Program.cs:21-28`) does **not** cancel the downstream CvJdAPI→ai-service call, which runs its full 5 minutes — a resource-leak shape worth raising to Duong independently of auth.

### (c) CvJdAPI `AI_SERVICE_URL` points at an unpublished port — CV/JD broken in container mode

**Evidence:** `source/CvJdAPI/appsettings.Development.json:8` = `http://localhost:8000`; same default hardcoded at `Endpoints/CvEndpoints.cs:48`, `JdEndpoints.cs:38`, `JdEndpoints.cs:96`, `MatchEndpoints.cs:30`. `infra/docker-compose.yml:19` publishes `"8080:8000"` — port 8000 is not host-reachable. All three other consumers correctly use 8080.

**Impact:** works in venv mode (`run-all.ps1:132` binds 8000 on the host), silently fails with connection-refused whenever ai-service runs in Docker — the documented default.

**Fix** (Duong, ~15 min): `8080` now; `http://ai-service:8000` under Phase 1. Consolidate the four hardcoded fallbacks to a single config read.

### Draft messages

Both drafts from the previous revision stand unchanged and are reproduced in `§0-messages` below. **Ownership caveat unchanged:** git authorship on `source/PaymentAPI` is mixed — `Duong Nguyen` (8), `Usopp204` (4), `HaoNam` (4). Confirm the PaymentAPI recipient before sending rather than inferring from commit counts.

---

## Phase 1 — Full-stack containerization

**Objective:** `git clone` → `docker compose up` on a machine with only Docker + NVIDIA drivers brings up the entire system: 11 .NET services + gateway + WebClient + ai-service + SQL Server.
**Closes:** criterion 7 (0→3), F-11, F-06, F-12, criterion 8 (1→2), criterion 3 (1→2).
**Effort: L.** **Ownership: SHARED + every service.**

### 1.1 Blocking prerequisite — no schema exists in the repo

Verified read-only:
- `grep -rl "CREATE TABLE" --include="*.sql"` → **zero results**. No base schema anywhere.
- Four incremental scripts on disk: `InterviewAPI/Sql/011_add_hint_used_at.sql`, `012_add_last_skip_at.sql`, `PaymentAPI/Sql/001_add_credit_ledger_bonus_index.sql`, `002_add_credit_wallets_userid_unique_index.sql`.
- **Only `001_…sql` is tracked.** The rest are excluded by `.gitignore:106` (`/source/*/Sql/`, *"SQL migrations shared via Zalo, not tracked"*).
- Only CvJdAPI self-creates schema (`CvJdAPI/Program.cs:68`, `EnsureCreated()`).

**Resolution, in order:**
1. Generate a per-database baseline by hand via **SSMS → Generate Scripts** (schema + seed/lookup data). Commit as `db/baseline/000_<Database>_baseline.sql`. Hand-driven SSMS output, not an EF migration — constraint honoured.
2. **Remove `.gitignore:106`** so `source/*/Sql/*.sql` is tracked. Without this every future migration is invisible to a clean clone and Phase 1 silently regresses.
3. Make every script idempotent (`IF NOT EXISTS`). `002_…sql` already demonstrates the pattern.

**Risk if skipped:** stack starts, every service crashes on first DB access. No partial credit.

### 1.2 Per-service .NET container specification

All 12 .NET projects. One `Dockerfile` per project directory.

**Pinned base images** (verify current patch on MCR before committing — these are the shape, not a currency guarantee):
- Build: `mcr.microsoft.com/dotnet/sdk:8.0.404-noble`
- Runtime: `mcr.microsoft.com/dotnet/aspnet:8.0.11-noble`

Chiseled variants are non-root by default and smaller, but ship no shell/`curl`, complicating `HEALTHCHECK`. Use plain `-noble` + explicit non-root user now; note chiseled as post-thesis hardening.

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

Uniform in-container port **8080** everywhere. Host-side 700x ports disappear inside the compose network.

**`.dockerignore`** (one per project, identical):
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
Excluding `.env*` is deliberate — half the F-12 fix. Secrets arrive as compose env vars, never baked into a layer.

### 1.3 SQL Server container

| Item | Specification |
|---|---|
| Image | `mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04` (pin; verify CU) |
| User | Non-root `mssql` (UID 10001) by default |
| Env | `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}`, `MSSQL_PID=Developer` |
| Volume | `mssql-data:/var/opt/mssql` |
| Healthcheck | `sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"`, `interval=10s retries=10 start_period=30s` |

**Forcing function:** the current password `12345` (`.env:1-11`) **will not start a SQL Server container** — the image enforces ≥8 chars with complexity and exits. Containerization mandates the strong-password fix, which is also part of criterion 8. Hard startup dependency, not optional cleanup.

**Init strategy** — one-shot `db-init` service:
```yaml
db-init:
  image: mcr.microsoft.com/mssql-tools:latest
  depends_on:
    sqlserver: { condition: service_healthy }
  volumes: [ "../db:/db:ro", "../source:/source:ro" ]
  entrypoint: ["/bin/bash", "/db/init.sh"]
  environment: [ "MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}" ]
  restart: "no"
```
`db/init.sh` (new): create each of the 11 catalogs, apply `db/baseline/000_*.sql`, then every `source/*/Sql/*.sql` in lexical order. All idempotent → re-run on an existing volume is a no-op.

Every application service declares `depends_on: db-init: { condition: service_completed_successfully }`. `service_healthy` on SQL Server alone would let services race schema creation.

**Connection strings** already read from `<X>_DB_CONNECTION_STRING` in all 11 `Program.cs` files — no code change, only the value changes to `Server=sqlserver,1433;…`. That existing discipline is what makes this phase tractable.

### 1.4 Exhaustive `localhost` → compose-DNS table

| # | File:line | Current | Becomes | Bucket |
|---|---|---|---|---|
| 1–10 | `ApiGateway/appsettings.json:343,350,360,367,374,381,388,395,402,409` | `http://localhost:7001–7010/` | `http://{authapi,userapi,cvjdapi,questionapi,interviewapi,evaluationapi,feedbackapi,paymentapi,newsapi,notificationapi}:8080/` | SHARED |
| 11 | `ApiGateway/Program.cs:36` | CORS default `http://localhost:5130` | `${CORS_ALLOWED_ORIGINS}` | SHARED |
| 12 | `UserAPI/Program.cs:49` | `new Uri("http://localhost:7001/")` **hardcoded in C#** | config `${AUTH_BASE_URL}` | NEEDS-TEAMMATE |
| 13 | `UserAPI/Program.cs:54` | `new Uri("http://localhost:7005/")` **hardcoded in C#** | config `${INTERVIEW_BASE_URL}` | NEEDS-TEAMMATE |
| 14 | `UserAPI/Program.cs:73` | `new Uri("http://localhost:7008/")` **hardcoded in C#** | config `${PAYMENT_BASE_URL}` | NEEDS-TEAMMATE |
| 15 | `WebClient/appsettings.json:4` | `http://localhost:7000/` | `http://apigateway:8080/` | MINE |
| 16 | `InterviewAPI/appsettings.json:14` | `http://localhost:8080` | `http://ai-service:8000` | MINE |
| 17 | `InterviewAPI/Program.cs:56` | fallback `http://localhost:8000` | env var, no literal fallback | MINE |
| 18 | `EvaluationAPI/appsettings.json:14` | `http://localhost:8080` | `http://ai-service:8000` | MINE |
| 19 | `EvaluationAPI/Program.cs:40` | fallback `http://localhost:8000` | env var, no literal fallback | MINE |
| 20 | `QuestionAPI/appsettings.json:10` | `http://localhost:8080` | `http://ai-service:8000` | NEEDS-TEAMMATE |
| 21 | `QuestionAPI/Program.cs:87` | fallback `http://localhost:8080` | env var, no literal fallback | NEEDS-TEAMMATE |
| 22 | `CvJdAPI/appsettings.Development.json:8` | `http://localhost:8000` (**broken today — §0(c)**) | `http://ai-service:8000` | NEEDS-TEAMMATE |
| 23–26 | `CvJdAPI/Endpoints/CvEndpoints.cs:48`, `JdEndpoints.cs:38`, `JdEndpoints.cs:96`, `MatchEndpoints.cs:30` | fallback literals | single config read | NEEDS-TEAMMATE |
| 27 | `.env:63` | `VNPay__ReturnUrl=http://localhost:7008/vnpay/return` | **stays a host URL** — do NOT convert | NEEDS-TEAMMATE |
| 28 | `.env:1-11` (×11) | `Server=localhost\MSSQLSERVER01` | `Server=sqlserver,1433` | SHARED |

Row 27 is the classic trap: VNPay redirects the **user's browser**, not a container — compose DNS would break the payment return leg.

### 1.5 Compose entry shape + **mandatory port lockdown** `[CONTRADICTED A-10 → now a hard gate]`

```yaml
interviewapi:
  build: { context: ../source, dockerfile: InterviewAPI/Dockerfile }
  container_name: devprep-interview
  networks: [devprep-internal-net]
  depends_on:
    db-init:    { condition: service_completed_successfully }
    ai-service: { condition: service_healthy }
  environment:
    - INTERVIEW_DB_CONNECTION_STRING=Server=sqlserver,1433;Database=DevPrep_Interview;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;
    - AiService__BaseUrl=http://ai-service:8000
    - INTERNAL_API_KEY=${INTERNAL_API_KEY}
    - Jwt__Key=${JWT_KEY}
  # NO ports: block — MANDATORY. Internal network only.
  restart: unless-stopped
```

**Non-negotiable rule:** exactly **two** services publish to the host — `apigateway` (`7000:8080`) and `webclient` (`5130:8080`). Every other service, including `sqlserver` and `rabbitmq` in production posture, has **no `ports:` block**. A `ports:` entry on any other service is a review-blocking defect, not a style preference: centralizing auth at the gateway while leaving service ports reachable would make the system *less* secure than today, because per-service JWT validation would no longer be the only thing standing in the way.

**Development exception, explicitly bounded:** `sqlserver` may publish `1433` and `rabbitmq` may publish `15672` **in a `docker-compose.override.yml` only**, never in the base file, so the committed default is locked down and the override is opt-in.

### 1.6 Order of operations — both run modes stay working throughout

| Step | Action | Local dev after |
|---|---|---|
| 1 | Commit baseline schema; un-ignore `source/*/Sql/` | Unaffected (additive) |
| 2 | Replace every literal (§1.4) with **env var + current localhost value as default** | Works — defaults reproduce today exactly |
| 3 | Add per-project `Dockerfile` + `.dockerignore` | Works — not read by `dotnet run` |
| 4 | Add `sqlserver` + `db-init`; point local `.env` at `Server=localhost,1433` | Works — local services use containerized SQL via the dev-override port |
| 5 | Add .NET services to compose one at a time, verifying each | Works — either mode; compose uses DNS, local uses defaults |
| 6 | Remove `.env.example` load from all 11 `Program.cs` (F-12) | Works — real `.env` still loads |
| 7 | Delete orphaned `source/Dockerfile` (F-11) | Works — never wired to anything |

Step 2 is the linchpin: every literal becomes *env-var-with-current-default*, so container and local mode diverge only by environment, never by code. Also the permanent F-06 fix.

### Acceptance criteria (board-verifiable, <2 min)
1. Clean machine: `git clone && cd infra && docker compose up -d`. No SQL Server install, no SSMS.
2. `docker compose ps` → **15 services** (14 + rabbitmq from Phase 4), all `healthy`, `db-init` `exited (0)`.
3. Browser → `localhost:5130` → log in → start interview → answer a question.
4. `docker compose exec apigateway curl -fsS http://interviewapi:8080/health` → 200 (proves compose DNS).
5. **Lockdown gate:** `curl http://localhost:7005/health` from the host → **connection refused**. Repeat for 7001–7010. Any success fails the phase.

**Biggest risk:** the baseline schema not matching what dev databases actually contain — schema has only ever existed as manually-applied SSMS state, so there is no authoritative source to diff against. Mitigation: generate from the most-current machine; each service owner smoke-tests against a fresh volume before the phase closes.
**Rollback:** revert the compose commit; step 2 preserves current defaults so application code keeps working. **Affects:** everyone, but only on opt-in; `run-all.ps1` remains the fallback.

---

## Phase 2 — Observability baseline

**Objective:** one request traceable across every service it touches.
**Closes:** criterion 9 (0→2), F-14. **Effort: M.** **Ownership: SHARED + per-service.**

| Action | Path | Reason | Bucket |
|---|---|---|---|
| MODIFY | `source/ApiGateway/Program.cs` | Generate `X-Correlation-Id` if absent; forward; log | SHARED |
| CREATE | `source/InterviewAPI/Middleware/CorrelationIdMiddleware.cs` | Inbound header → `ILogger` scope | MINE |
| CREATE | `source/EvaluationAPI/Middleware/CorrelationIdMiddleware.cs` | Same | MINE |
| CREATE | `source/{Interview,Evaluation}API/Http/CorrelationForwardingHandler.cs` | One `DelegatingHandler` registered once per service — not per-client edits | MINE |
| MODIFY | `ai-service/app/main.py` | FastAPI middleware: accept + echo + log | MINE-BUT-SHARED |
| MODIFY | 11 × `Program.cs` `/health` | Real DB connectivity check → readiness (F-14) | MIXED |
| MODIFY | `*.csproj` | `Serilog.AspNetCore` + console JSON sink | MIXED |

Serilog is a new library. `CLAUDE.md` names SignalR/MediatR/AutoMapper/FluentValidation as forbidden; a logging sink is a different category, but **flag for team agreement** rather than assume. Fallback if rejected: `ILogger` scopes + built-in `AddJsonConsole()` — zero new packages, ~80% of the benefit.

**Acceptance:** (1) `curl -H "X-Correlation-Id: demo-123" …` (2) `docker compose logs | grep demo-123` shows the same id in ApiGateway, InterviewAPI, EvaluationAPI, ai-service. (3) Stop SQL Server → InterviewAPI `/health` → 503 within 30 s.
**Risk:** a forgotten forwarding point yields a broken trace that reads worse than none — hence the single `DelegatingHandler` rather than per-client edits.
**Rollback:** additive; remove registration lines.

---

## Phase 3 — Service-to-service trust consolidation + **F-17 exposure closure**

**Objective:** one definition of "internal call," fail-closed everywhere; gateway rejects anonymous traffic; ai-service no longer publicly exposed unauthenticated.
**Closes:** criterion 11 (0→3), criterion 5 (1→3), **F-17 (High, new)**, F-02, part of F-03, F-13.
**Effort: M.** **Ownership: MINE + MINE-BUT-SHARED + SHARED + NEEDS-TEAMMATE.**

### 3.1 NEW FINDING — **F-17: ai-service is publicly exposed through an undocumented cloudflared tunnel with most routes unauthenticated**

**Severity: High.** (Not Critical — see the mitigating factors below. Would become Critical if the tunnel were made persistent, or if `INTERNAL_API_KEY` were ever left unset.)

**Evidence — what is exposed.** A cloudflared Quick Tunnel is run manually to give teammates without a GPU access to `ai-service` on host port 8080 (`infra/docker-compose.yml:19`, `"8080:8000"`). Verified read-only, the router-level auth posture is:

| Route prefix | Source | Auth |
|---|---|---|
| `/parse/cv`, `/parse/jd`, `/parse/jd-file` | `app/cv/router.py:13`, `app/jd/router.py:13` | **NONE** |
| `/match` | `app/match/router.py:6` | **NONE** |
| `/interview/*` (start, answer, voice/answer, hint, skip, end, report, transcript, tts) | `app/api/interview.py:28` | **NONE** |
| `/evaluate` | `app/api/evaluation.py:13` | **NONE** |
| `/catalog` | `app/api/catalog.py:12` | **NONE** |
| `/ui` (static test UI) | `app/main.py:71` | **NONE** |
| `/internal/*` | `app/api/internal.py:15` + per-endpoint `_check_key` | Key — **fails open if key empty** (`internal.py:21-24`) |
| `/internal/biometrics/*` | `biometrics/router.py:36-37`, `dependencies=[Depends(require_internal_api_key)]` | Key at router level (same fail-open caveat) |

CORS is wide open: `.env:40` `ALLOWED_ORIGINS=*` → `app/main.py:45-51`.

**Why this is more serious than F-05.** F-05 is a worse *defect* but is local-only. F-17 is genuine **public internet exposure** of:
1. **PII ingestion with no auth** — `/parse/cv` accepts PDF/DOC/DOCX uploads (`app/cv/router.py:25-29`) and runs them through the GPU parser. Anyone with the URL can upload arbitrary documents; anyone can use it as a free CV-parsing service.
2. **Unmetered GPU compute** — every unauthenticated route above runs on the single shared GPU. Combined with `--workers 1` (`Dockerfile:77`), a handful of concurrent requests can starve the whole service, including biometrics and interview grading for legitimate users (this is the same fault-isolation weakness as F-16).
3. **Latent escalation** — if `INTERNAL_API_KEY` is ever unset in the tunneled environment, the fail-open branches at `internal.py:21-24` and the biometrics router silently expose face/voice embedding endpoints and the RAG index to the same public URL.

**Mitigating factors, weighed explicitly.** The tunnel is **not documented anywhere in the repo** — the only trace is `.gitignore:77` (`infra/cloudflared.deb`); there is no `cloudflared` service in compose, no tunnel config file, no reference in `run-all.ps1`. It is started by hand, so exposure is **intermittent, not continuous**, and a Quick Tunnel URL is random and ephemeral (`*.trycloudflare.com`), so it is unlikely to be discovered by scanning. **That intermittency is exactly why this is High rather than Critical — and also why it is easy to under-rate:** because it leaves no artifact in the repo, nothing in code review or CI will ever surface it, and the next person to start a tunnel will not know the surface they are opening.

**Remediation — ordered by what unblocks fastest.**

**Immediate operational mitigation (no code, no coordination, doable today):** stop tunneling `ai-service:8080` directly. Either (a) tunnel `apigateway:7000` instead, so all traffic traverses the gateway's controls, or (b) put Cloudflare Access in front of the existing tunnel so only authenticated teammates reach it, or (c) run the tunnel only while actively needed and tear it down after. Any of the three removes the public unauthenticated surface without touching a line of code. **This should happen before the phase work, independent of scheduling.**

**Code changes (MINE — no team sign-off needed):**

| Action | Path | Reason | Bucket |
|---|---|---|---|
| MODIFY | `ai-service/app/api/internal.py:21-24` | Remove fail-open; fail closed on empty key | MINE-BUT-SHARED |
| MODIFY | `ai-service/biometrics/` key check | Same fail-closed treatment | MINE-BUT-SHARED |
| MODIFY | `ai-service/app/core/config.py` | `ALLOWED_ORIGINS` must not default to `*` | MINE-BUT-SHARED |
| MODIFY | `ai-service/app/main.py:67-71` | Do not mount `/ui` unless an explicit `ENABLE_TEST_UI` flag is set | MINE-BUT-SHARED |
| CREATE | `docs/architecture/ai-service-exposure.md` | Document the tunnel, what it exposes, and the rule that it fronts the gateway — closes the "undocumented" half of the finding | MINE |

**Coordination-gated change — `/parse/*` and `/match` authentication:**
These three routes are **Duong's consumers** (CvJdAPI calls them, §0(c)). Adding `Depends(require_internal_api_key)` to `app/cv/router.py:13`, `app/jd/router.py:13`, `app/match/router.py:6` will **break CvJdAPI** unless CvJdAPI's `HttpClient` is simultaneously updated to send `X-Internal-Api-Key` — and CvJdAPI's client (`CvJdAPI/Program.cs:22-25`, `AddHttpClient("ai")`) currently sends **no** internal key at all, unlike every other consumer.

So this is a **two-sided change requiring a coordinated landing with Duong**, sequenced:
1. Duong adds the header to CvJdAPI's `"ai"` HttpClient (tolerated by ai-service, which ignores it while unauthenticated).
2. I add the router dependency to the three `/parse`+`/match` routers.
3. Joint verification of `/parse/cv`, `/parse/jd`, `/match` (the regression gate already defined in Phase 7).

`/interview/*`, `/evaluate`, `/catalog` have **no external consumer** — per `CLAUDE.md` the .NET flow uses `/internal/*` exclusively — so those can be key-protected unilaterally. **Verify before landing** that nothing else calls them.

### 3.2 Internal-auth tier consolidation `[CONFIRMED A-5]`

Two tiers, deliberately: **`X-Internal-Api-Key` = service-to-service; JWT + role = human callers via WebClient.** The defect is the blurred boundary, not the two tiers — so AuthAPI's `InternalAccountsController` (`:24-25`, `[Authorize(Roles="Admin,Manager")]`) is **correct as tier 2** and needs no change. What must change is that tier-1 checks stop failing open, and that the tiers are written down.

| Action | Path | Reason | Bucket |
|---|---|---|---|
| MODIFY | `QuestionAPI/Controllers/InternalQuestionController.cs:40-45` | Remove `if (string.IsNullOrEmpty(_internalApiKey)) return true;` | NEEDS-TEAMMATE |
| MODIFY | `QuestionAPI/Program.cs` | Refuse startup without `INTERNAL_API_KEY`, per `NotificationAPI/Program.cs:47-56` | NEEDS-TEAMMATE |
| MODIFY | `source/ApiGateway/Program.cs` | Add JWT validation at edge — **additive**; per-service validation stays `[A-10]` | SHARED |
| DELETE | `AddCors`/`UseCors` in 7 backend services | Unreachable by browsers once compose-internal (F-13) | MIXED |
| CREATE | `docs/architecture/internal-auth-convention.md` | Write down the two tiers and which endpoints belong to which | SHARED |

**Prerequisites:** Phase 1 (lockdown makes edge auth meaningful). Team decision gate on JWT centralization `[CONFIRMED A-9 — full-team sign-off; I draft, team decides]`.

**Acceptance criteria:**
1. Unset `INTERNAL_API_KEY` for QuestionAPI → container **fails to start** with a clear message.
2. `curl http://localhost:7000/api/interview/sessions` with no token → **401 at the gateway**; show the request absent from InterviewAPI logs. Same call with a valid token → 200.
3. **F-17 gate:** from outside the compose network, `curl <ai-service-url>/parse/cv` → 401, `/interview/start` → 401. `/health` still 200.
4. **Lockdown gate re-verified** (from Phase 1 criterion 5) — still refused.

**Risk:** gateway auth can break legitimately anonymous routes (login, register, public news). Mitigation: explicit anonymous allowlist (`/api/auth/login`, `/api/auth/register`, `/api/news/*`) enumerated and tested before enabling. Second risk: the `/parse/*` change breaks Duong's flow if landed out of order — hence the three-step sequence above.
**Rollback:** remove gateway `UseAuthentication` (per-service validation still present → posture returns to today's, not worse). ai-service router dependencies revert independently.

---

## Phase 4 — Async messaging reference implementation

**In core scope** per Q16 — "one real async flow" is explicitly part of the target, and it is the single most likely board probe after containerization.
**Closes:** criterion 4 (1→2), criterion 6 (1→2), F-07, part of F-08. **Effort: M.** **Ownership: MINE + SHARED.**

### Chosen flow: InterviewAPI → EvaluationAPI grading trigger
Both endpoints are MINE — zero teammate coordination, cannot be blocked — and it replaces the worst chain in the audit (F-08's 4-hop `Browser→GW→Interview→Evaluation→ai-service`).

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

**Contract library:** `contracts/interview-events/InterviewEvents.csproj`, mirroring the proven `contracts/auth/auth.csproj` pattern (already referenced by AuthAPI, UserAPI, NotificationAPI). Referenced by InterviewAPI + EvaluationAPI only. No new structural convention.

**Outbox:**
- `CREATE source/InterviewAPI/Models/OutboxMessage.cs` + `OutboxMessages` table in `DevPrep_Interview` — **same database**, so the insert is atomic with the `ChatMessages` write in one `SaveChanges`. No distributed transaction, no lost event.
- `CREATE source/InterviewAPI/Services/Outbox/OutboxPublisherService.cs` — `BackgroundService` polling unpublished rows → MassTransit publish → mark `PublishedAt`. Model shutdown/retry discipline on the existing `GradingBackgroundService.cs`.
- `CREATE source/InterviewAPI/Sql/013_create_outbox_messages.sql` — hand-authored, idempotent.
- `CREATE source/EvaluationAPI/Consumers/AnswerSubmittedForGradingConsumer.cs` — idempotent on `(SessionQuestionId, AttemptId)` `[ASSUMED A-6]`.

**Degradation when the broker is down** — the question a board will ask:
1. **Publish side:** outbox rows are written inside the business transaction and accumulate unpublished. Zero loss; the user-facing request never fails on broker state.
2. **Consume side:** the existing in-process `GradingBackgroundService` **stays** behind a flag (`Grading:UseBroker`). Broker unavailable → grading continues via today's path.
3. **Recovery:** on broker return, the publisher drains the backlog in order.

Keeping both paths is deliberate: it makes the phase reversible and gives a *live demo* of graceful degradation rather than a claim about it.

```yaml
rabbitmq:
  image: rabbitmq:3.13-management-alpine   # pin; verify current
  networks: [devprep-internal-net]
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
    interval: 15s
    retries: 5
  volumes: [rabbitmq-data:/var/lib/rabbitmq]
  # management UI published via docker-compose.override.yml only — see §1.5
```

**Acceptance:** (1) Submit an answer → management UI shows publish + consume. (2) `docker compose stop rabbitmq` → submit another → **request still returns 200**, outbox row count increments, grading completes via fallback. (3) `start rabbitmq` → backlog drains, **no duplicate `AnswerEvaluations` row**.
**Risk:** MassTransit is a significant new dependency; `CLAUDE.md`'s no-new-libraries rule doesn't name it and the broker decision authorizes it, but flag it to the team. Fallback: raw `RabbitMQ.Client`.
**Rollback:** `Grading:UseBroker=false`. Outbox keeps recording, publisher idles, in-process grading resumes. Affects only my two services.

---

## Phase 5 — Resilience and cross-service consistency

**Closes:** criterion 10 (1→3), F-09/F-10, rest of F-08. **Effort: S–M** (down from M — see A-7b). **Ownership: MINE + NEEDS-TEAMMATE.**

### 5.1 Justification — production-intent framing (use this wording, not "real money")

> VNPay is currently pointed at the sandbox endpoint (`.env:62`) and no live credentials exist. That is precisely why the failure mode must be fixed **now**: the refund path today logs and forgets (`PaymentApiClient.cs:104-112`), so a failed refund is silently lost with no automated recovery. A silent-loss failure mode on a credit ledger is not something to ship and then switch to live payments behind — the correctness has to exist *before* the sandbox-to-live switch, not after. We are fixing it while it is cheap and while the blast radius is zero.

This is the honest argument and the stronger one. **Do not claim live user funds are at risk** — a reviewer can open `.env` in ten seconds.

| Action | Path | Reason | Bucket |
|---|---|---|---|
| MODIFY | `source/InterviewAPI/Program.cs:56-104` | Polly circuit breaker + retry-with-jitter on 5 typed clients | MINE |
| MODIFY | `source/EvaluationAPI/Program.cs:43-60` | Same for 2 clients | MINE |
| MODIFY | `source/InterviewAPI/Clients/PaymentApiClient.cs:164-187` | `GetBalanceAsync` must not return `0` on failure — discriminated result so "no credits" ≠ "PaymentAPI down" | MINE |
| CREATE | `source/PaymentAPI/Sql/003_credit_ledger_reference_unique.sql` | Unique index on `(ReferenceId, EntryType)` `[ASSUMED A-6]` | NEEDS-TEAMMATE |
| MODIFY | `source/PaymentAPI/Services/InternalCreditService.cs` | Honour the constraint; return existing row on duplicate | NEEDS-TEAMMATE |
| MODIFY | `source/InterviewAPI/Services/Outbox/` | Failed refunds become durable outbox rows, not log lines `[CONFIRMED A-1]` | MINE |
| CREATE | orphan-account sweep (`IHostedService`) | Find accounts with no profile; re-invoke `CreateProfileIfNotExistsAsync` `[ASSUMED A-8]` | NEEDS-TEAMMATE |

**Sweep simplification `[A-7b]`:** `UserAPI.CreateProfileIfNotExistsAsync` (`Services/UserProfileService.cs:38`) is **idempotent by construction**. The sweep therefore only needs to **find** orphans and re-invoke it — no de-duplication, no "was this already fixed" bookkeeping, no compensation logic. Re-invoking on a non-orphan is a safe no-op. This is what drops the phase from M to S–M.

`PaymentApiClient.GetBalanceAsync` returning `0` on any HTTP failure (`:175`) is the most dangerous line in my own code — a PaymentAPI outage is indistinguishable from an empty wallet, so users see "insufficient credits" instead of "service unavailable." MINE and unblocked.

**Acceptance:** (1) `docker compose stop paymentapi` → start interview → *"Credit service unavailable"*, **not** *"insufficient credits."* (2) Same deduct twice with one `ReferenceId` → exactly one ledger row. (3) Stop PaymentAPI mid-session → refund writes an outbox row → restart → refund completes automatically.
**Risk:** wrong circuit-breaker thresholds mask real problems behind fast failures. Mitigation: log every state transition at Warning; Phase 2 correlation ids make traces legible.
**Rollback:** Polly policies are registration lines. The `GetBalanceAsync` signature change touches `InterviewSessionController.cs:612` — revert together.

---

## Phase 6 — Restore data ownership (cross-database SQL removal)

**In core scope** per Q16 — database-per-service is named explicitly, and it is the criterion the board's verdict most directly targets. Cutting it would waste the containerization work by leaving the strongest counter-evidence in place.
**Closes:** criterion 2 (1→3), criterion 1 (2→3), F-01 (Critical). **Effort: M–L.** **Ownership: NEEDS-TEAMMATE (entirely).**

| Action | Path | Reason | Bucket |
|---|---|---|---|
| MODIFY | `PaymentAPI/Program.cs:456-460` | Replace `DevPrep_Auth`/`DevPrep_User` join with batch internal calls | NEEDS-TEAMMATE |
| MODIFY | `PaymentAPI/Controllers/ManagerPaymentsController.cs:210-215` | Same | NEEDS-TEAMMATE |
| MODIFY | `UserAPI/Controllers/ManagerUsersController.cs:59-92,171-175,213-229,255-266` | Replace joins into `DevPrep_Auth`/`DevPrep_Interview`/`DevPrep_Payment` | NEEDS-TEAMMATE |
| CREATE | `source/InterviewAPI/Controllers/InternalSessionStatsController.cs` | Batch endpoint UserAPI needs — **mine, pre-buildable** | MINE |
| CREATE | PaymentAPI + AuthAPI batch internal endpoints | Symmetric | NEEDS-TEAMMATE |

`[ASSUMED A-4]` all new internal endpoints are **batch** (`POST` + id list → keyed result), mirroring `AccountLookupRequest`/`AccountLookupItem` in `AuthAPI/DTOs`. Per-row endpoints would turn one SQL query into N HTTP calls on an admin list page.

**Unblocking move:** the InterviewAPI batch endpoint UserAPI needs is mine. Building it first turns the teammate's task into "call this existing endpoint" instead of "design and build two things."

**Acceptance:** (1) `grep -rE "DevPrep_\w+\.dbo\." source/ --include=*.cs` → **zero results**. (2) Admin user-list renders equivalent data. (3) Page load within ~2× baseline (guards the N+1 regression).
**Risk:** highest-regression phase — it rewrites working admin queries. Mitigation: schedule after Phase 2 so there is observability to debug it; one query at a time; before/after screenshots.
**Rollback:** per-query revert; each change independent. Affects admin/manager pages only, not the candidate flow.

---

## Phase 7 — F-16: ai-service bundles four capabilities in one process

**Recorded in the previous revision; unchanged in substance.** Now also carries the F-17 regression gate.

`ai-service` is one FastAPI process (`app/main.py:43`) mounting eight routers spanning four capabilities consumed by four .NET services: RAG/interview (InterviewAPI), evaluation (EvaluationAPI), biometrics (InterviewAPI), CV/JD parsing (**CvJdAPI — Duong**). All load in one lifespan (`main.py:29-40`) under `--workers 1` (`Dockerfile:77`).

**Real costs, stated before the board can:** no bulkhead (a 5-minute CV parse can stall interview grading — the same weakness F-17 lets an anonymous caller trigger); single point of failure for four consumers; deploy coupling with Duong; `--reload` shipped in the production `CMD`.

**Recommendation: keep merged this cycle.** Board justification:

> ai-service is not a business bounded context — it is a **shared capability provider**, architecturally equivalent to a database or a cache. Microservice decomposition applies to business capabilities needing independent evolution; the four *business* contexts (Interview, Evaluation, Document, Question) are already separate .NET services owning their own data and lifecycle. ai-service is their shared *implementation detail* behind a stable internal contract.
>
> Splitting it would be actively harmful under our constraints. All four capabilities share one physical GPU, one CUDA context, and one set of memory-mapped weights. Four processes would multiply resident VRAM beyond the demo hardware, or force CPU fallback and eliminate the acceleration the system is built around. The gain is theoretical isolation; the cost is a system that no longer runs. We therefore treat it as infrastructure and address the real problem — fault isolation — directly.

**Mitigations instead** (MINE-BUT-SHARED — notify Duong; all three CV/JD routes preserved):

| Action | Path | Reason |
|---|---|---|
| MODIFY | `ai-service/Dockerfile:77` | Remove `--reload` from production `CMD` |
| MODIFY | `ai-service/Dockerfile:11` | Pin `nvidia/cuda:12.1.1-runtime-ubuntu22.04` by digest |
| MODIFY | `ai-service/Dockerfile` | Add non-root user + `HEALTHCHECK` (currently neither) |
| CREATE | `ai-service/app/core/concurrency.py` | Per-capability `asyncio.Semaphore`; CV/JD capped so it cannot starve interview/biometrics |
| MODIFY | `ai-service/Dockerfile:58-66` | Stop baking the Piper voice; mount via volume per the weights-in-volumes decision |
| CREATE | `docs/architecture/ai-service-split-criteria.md` | Split triggers: second GPU, CV/JD sustained >20% GPU time, or Duong needing an independent release cadence. Biometrics splits first |

**Acceptance:** (1) Concurrent CV parse + interview answer → both complete; interview latency stays in band. (2) `docker inspect` shows non-root + `HEALTHCHECK`. (3) **Duong's regression gate:** `/parse/cv`, `/parse/jd`, `/match` unchanged — now including the `X-Internal-Api-Key` header added in Phase 3.
**Rollback:** all additive/config; revert the Dockerfile commit. **Affects Duong** — coordinate before landing.

---

## Phase 8 — API contracts, versioning, and StatisticsAPI removal

**Closes:** criterion 12 (1→2), F-04. **Effort: S.** **Ownership: SHARED.**

| Action | Path | Reason |
|---|---|---|
| CREATE | `docs/architecture/api-versioning-policy.md` | Additive-only for internal APIs; `/v2/` URL segment for breaking changes |
| MODIFY | `contracts/README.md` | Extend beyond the 3 JSON schemas to REST endpoints |
| MODIFY | `docs/architecture/pure-microservices-baseline.md:39`, `contracts/README.md:31` | Remove StatisticsAPI `[CONFIRMED A-11]` |
| DELETE | `source/StatisticsAPI/` | Stub removal `[A-11b]` |

**`[A-11b]` — the deletion is simpler than first planned.** Verified: `source/StatisticsAPI/` is **not referenced in `DevPrep.sln`**, contains **no `.csproj`**, and is **entirely untracked** (`git ls-files -- source/StatisticsAPI/` → empty; only `bin/`/`obj/` build artifacts). So:
- Deleting it is a **local filesystem cleanup** — zero git diff, zero build risk, no solution edit needed.
- The **only committed change in F-04 is the two doc edits.**
- Because it is untracked, the deletion is **not recoverable from git** — but there is nothing of value to recover (compiled artifacts from a project whose source no longer exists).

**Acceptance:** board asks "what happens on a breaking change?" → a written policy exists. `ls source/StatisticsAPI` → not found. **Rollback:** docs revert; the stub has no consumers.

---

## Team ask — consolidated agenda by owner

> **Ownership caveat:** git authorship is genuinely mixed (every service has 3+ contributors in the sampled range). Confirm before sending — particularly PaymentAPI, where `Duong Nguyen` has the most commits (8) despite Duong being identified as the CvJdAPI owner.

### Usopp204 — *(git: top author on UserAPI, EvaluationAPI, ApiGateway, WebClient)*
| Change | Why | Effort | If skipped |
|---|---|---|---|
| UserAPI: 3 hardcoded URLs → config (`Program.cs:49,54,73`) | Blocks containerization | S (30 min) | **UserAPI cannot run in compose.** Phase 1 fails |
| UserAPI: remove cross-DB SQL (4 sites) | F-01 Critical | M (1 day) | Criterion 2 stays 1/3; the board's core criticism goes unanswered |
| Review + sign off gateway JWT centralization | Widest blast radius; needs team decision `[A-9]` | S (review) | Phase 3 blocked |

### DHHaoNam — *(git: top author on AuthAPI, NotificationAPI, NewsAPI)*
| Change | Why | Effort | If skipped |
|---|---|---|---|
| QuestionAPI: remove fail-open internal auth (`InternalQuestionController.cs:40-45`) | F-02 | S (20 min) | One misconfiguration exposes the full question bank + reference answers |
| AuthAPI/NewsAPI/NotificationAPI: Dockerfile + `.dockerignore` (template in §1.2) | Phase 1 | S (1 h each) | Those services absent from compose; "full system" claim fails |
| Orphan-account sweep in AuthAPI or UserAPI | F-10 | S (simplified — target is idempotent, `[A-7b]`) | Orphaned accounts accumulate silently |

### Duong — *(CvJdAPI owner; git: top author on CvJdAPI and PaymentAPI)*
| Change | Why | Effort | If skipped |
|---|---|---|---|
| CvJdAPI: `AI_SERVICE_URL` → 8080 / compose DNS (5 sites) | **Broken today in Docker mode** — §0(c) | S (15 min) | CV/JD silently fails whenever ai-service is containerized |
| CvJdAPI: send `X-Internal-Api-Key` on the `"ai"` HttpClient (`Program.cs:22-25`) | **Prerequisite** for closing F-17 on `/parse/*` | S (15 min) | `/parse/*` must stay public, or CvJdAPI breaks |
| CvJdAPI: add JWT auth (F-15) | Unauthenticated GPU access | S–M (1 h) | Anonymous GPU abuse; criterion 11 capped |
| Review ai-service changes (Phases 3 + 7) | Shared process; his 3 routes must keep working | S (review) | Risk of breaking `/parse/cv`, `/parse/jd`, `/match` |
| PaymentAPI `/credits/balance` auth (F-05) — **if he owns PaymentAPI** | Critical severity, local-only reach | S (30 min) | Auth-bypass defect ships |

---

## Board-defence mapping

| Likely board question | Phase | What to show on screen |
|---|---|---|
| "Is this deployable as microservices, or just projects in one solution?" | **1** | Clean machine: `docker compose up -d`, `docker compose ps` — 15 services healthy, no SQL Server on the host |
| "Show me database-per-service is real, not just separate connection strings" | **6** | `grep -rE "DevPrep_\w+\.dbo\." source/` → zero results, contrasted with the audit's before-state |
| "Are your services actually isolated, or just on different ports?" | **1, 3** | `curl http://localhost:7005/health` → refused. Only 7000 and 5130 answer |
| "What happens when one service goes down?" | **5** | `docker compose stop paymentapi` live → "service unavailable," rest of system keeps working |
| "Where is your asynchronous communication?" | **4** | RabbitMQ UI showing publish + consume during a live answer; then stop the broker and show the request still succeeding |
| "How do you trace a request across services?" | **2** | One `curl` with a correlation id → `docker compose logs \| grep` showing it in 4 services |
| "How do services authenticate to each other?" | **3** | Unset `INTERNAL_API_KEY` → service refuses to start. Gateway call without a token → 401 at the edge |
| "Why is ai-service one process doing four things?" | **7** | The written justification + live concurrent CV parse and interview answer not blocking each other |
| "What's your policy when an API contract changes?" | **8** | `docs/architecture/api-versioning-policy.md` |
| "Do you understand your own weaknesses?" | **the audit** | `microservice-audit.md` — self-identified 11/36 with evidence, plus F-17 found by follow-up investigation. Strongest available answer: we found these before you did |

---

## Projected scoring — full scope

| Criterion | Now | Target | Driver |
|---|---|---|---|
| 1 Decomposition | 2 | **3** | Phase 6 + StatisticsAPI removal |
| 2 Database-per-service | 1 | **3** | Phase 6 |
| 3 Independent deployability | 1 | **2** | Phase 1 (capped at 2 — per-service CI/CD explicitly out of scope) |
| 4 Inter-service communication | 1 | **2** | Phase 4 (one async flow, not all flows) |
| 5 Gateway & exposure | 1 | **3** | Phase 1 + 3, incl. mandatory port lockdown |
| 6 Data consistency | 1 | **2** | Phase 4 + 5 (outbox on the reference flow) |
| 7 Containerization | 0 | **3** | Phase 1 |
| 8 Config & secrets | 1 | **2** | Phase 1 (capped at 2 — no vault/secret manager) |
| 9 Observability | 0 | **2** | Phase 2 (capped at 2 — no distributed tracing or metrics backend) |
| 10 Resilience | 1 | **3** | Phase 5 |
| 11 Service-to-service security | 0 | **3** | Phase 3 + §0 + F-17 closure |
| 12 API contracts | 1 | **2** | Phase 8 |

**Total: 11/36 → 30/36 (≈83%).** The three criteria currently at **0** — the ones most responsible for the board's verdict — all reach 2 or 3.

### If time collapses — cut order (do not plan around this)

Recorded only as a contingency; the plan is shaped for full scope.

1. **Cut Phase 8 first** (−1 → 29). Docs and a stub deletion; lowest board-visible value per hour.
2. **Then Phase 6** (−3 → 26). Highest score value of anything remaining, but also the highest regression risk and the one most dependent on teammate availability. Cutting it means the board's central criticism stays partly unanswered — cut reluctantly.
3. **Then Phase 4** (−2 → 24). Leaves "where is your async messaging?" without a live answer. Phases 1, 2, 3, 5 alone still yield **24/36 (67%)** and close every Critical security finding.

Never cut Phase 1 — everything else assumes it, and it is the phase the board's verdict most directly targets.

---

## §0-messages — teammate drafts

### To the PaymentAPI owner (confirm recipient first)

> Subject: PaymentAPI /credits/balance — authentication gap
>
> Hi — while doing an architecture audit of the repo I found something on PaymentAPI worth a quick look.
>
> `GET /credits/balance` (`PaymentAPI/Program.cs:235-251`) takes the user identity from the `X-User-Id` request header and queries that user's balance. The route has no `[Authorize]` attribute and isn't covered by the `/api/internal/*` key middleware at `Program.cs:139-169`, because that middleware matches on the `/api/internal` prefix and this path doesn't start with it.
>
> The gateway forwards `/api/payment/{**catch-all}` with the prefix stripped, so `GET /api/payment/credits/balance` with any `X-User-Id` value returns that user's balance, total purchased, and last-updated timestamp — without logging in. Good news: I checked and the cloudflared tunnel we use only fronts ai-service, not the gateway, so this is reachable on the local network but not from the internet. Worth fixing before that ever changes, though.
>
> There are two callers to keep working: WebClient (`WebClient/Services/Payment/PaymentService.cs:52-54`, which already sends a JWT via its delegating handler) and InterviewAPI (`InterviewAPI/Clients/PaymentApiClient.cs:164-181`, which sends only the header because it's acting on the user's behalf server-side).
>
> Suggestion — split it so both keep working:
> 1. `GET /credits/balance`: add `[Authorize]`, take the user id from the token subject, ignore `X-User-Id`.
> 2. Add `/api/internal/credit/balance` for service callers — it gets the existing fail-closed key protection automatically.
>
> I'll update the InterviewAPI side to use the new internal route as soon as (2) exists — that half is mine. Happy to pair on it if easier.

### To Duong — covers §0(b), §0(c), and the F-17 prerequisite

> Subject: CvJdAPI — AI_SERVICE_URL port, endpoint auth, and a heads-up on ai-service
>
> Hi Duong — a few things I found in CvJdAPI during an architecture audit. The first one probably explains any CV/JD failures you've seen when running ai-service in Docker.
>
> **1. AI_SERVICE_URL port mismatch.** `CvJdAPI/appsettings.Development.json:8` points at `http://localhost:8000`, and the same default is repeated at four call sites (`Endpoints/CvEndpoints.cs:48`, `Endpoints/JdEndpoints.cs:38` and `:96`, `Endpoints/MatchEndpoints.cs:30`). But docker-compose publishes ai-service as `8080:8000` (`infra/docker-compose.yml:19`) — port 8000 isn't reachable from the host. So CV/JD parsing works in venv mode but gets connection-refused whenever ai-service runs in its container. The other three consumers (Interview, Evaluation, Question) all use 8080. Quick fix is `8080`; when we containerize the .NET services it becomes `http://ai-service:8000` via compose DNS.
>
> **2. Internal API key on the "ai" HttpClient.** `CvJdAPI/Program.cs:22-25` registers the ai-service client without an `X-Internal-Api-Key` header — every other consumer sends one. I'd like to put authentication on ai-service's `/parse/cv`, `/parse/jd`, and `/match` routes (they're currently open to anyone who can reach the service, and the cloudflared tunnel we run for GPU access makes that the public internet while it's up). That change will break CvJdAPI unless the header goes on first. Could you add it? Then I'll add the check on the ai-service side, and we verify the three routes together.
>
> **3. No auth on the CvJdAPI endpoints.** `CvJdAPI/Program.cs` doesn't call `AddAuthentication`/`AddAuthorization`, and the endpoints at `Program.cs:61-63` have no `RequireAuthorization()`. Since the gateway doesn't authenticate either, CV/JD parsing is callable without logging in — and each call can hold the GPU for up to 5 minutes (`Program.cs:22-25` timeout). If that's deliberate because the service is still a stub, no problem — just tell me and I'll note it as a known gap rather than a finding.
>
> Also flagging: ai-service is shared between us — my Interview/Evaluation flows and your three routes run in the same process and image. I'm planning changes there (Dockerfile pinning, removing `--reload`, per-capability concurrency limits). I'll make sure your routes keep working and will ping you before anything lands.
