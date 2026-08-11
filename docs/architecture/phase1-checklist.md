# Phase 1 — Containerization Execution Checklist

**Companion to:** `docs/architecture/microservice-remediation-plan.md` (Phase 1) and `docs/architecture/schema-recovery.md` (the §1.1 TIER 3 item).
**Design goal:** local `run-all.ps1` / venv mode keeps working at **every** commit boundary. See §0 for how that is guaranteed and the one task that would break it if done wrong.
**Resumable:** batches are independent and ordered by dependency. Stop after any batch; the tree is always in a working state.

---

## 0 — The rule that keeps local dev alive

**Never repoint the root `.env` at the container.** Two env files, two modes:

| File | Used by | DB target | Committed? |
|---|---|---|---|
| `.env` (root) | `run-all.ps1`, `dotnet run`, venv ai-service | `localhost\MSSQLSERVER01` — **unchanged throughout Phase 1** | No (gitignored) |
| `infra/.env.docker` (**new**) | `docker compose` only | `sqlserver,1433` | No — but `infra/.env.docker.example` is |

Every config change in this checklist follows the same shape: **read from an env var, default to today's literal value.** Container mode overrides via compose; local mode falls through to the default and behaves exactly as it does now.

> **The single task that breaks venv mode if done wrong: Batch I.** If the root `.env` connection strings are edited to point at `sqlserver,1433` or the sa password is changed there, `run-all.ps1` stops working immediately. Batch I is written to avoid this — container values go in `infra/.env.docker`, never the root `.env`. If you find yourself editing `.env:1-11`, stop.

**No other batch in the unblocked section can break local dev.** Verified by construction: every change is either a new file (Dockerfile, `.dockerignore`, compose entry) or an env-var indirection with an identical default.

---

## Unblocked work — start immediately, no schema needed

### Batch A — per-Dockerfile ignore files (**revised — see blocker B-1**)
**Branch:** `chore/docker-ignore-files` · **TIER 1** · **Blocked by:** nothing · **Blocks:** Batch B

> **Revised after pre-flight.** The original plan put one `.dockerignore` in each project directory with the build context set to `source/`. Both halves were wrong — see **Blocker B-1** below. The build context must be the **repo root**, and a root `.dockerignore` already exists that would sabotage it.

**The problem:** `.dockerignore:11` contains `source/`. That file was written for the ai-service build (context `../ai-service`), where excluding `source/` is correct. But once the .NET build context is the repo root, that single line excludes **the entire application** from every .NET image build.

**The fix:** BuildKit supports a per-Dockerfile ignore file named `<dockerfile-path>.dockerignore`, which takes precedence over the root `.dockerignore` for that build only. Leave the root file untouched (ai-service still needs it).

| Paths | Action |
|---|---|
| `source/{ApiGateway,AuthAPI,UserAPI,CvJdAPI,QuestionAPI,InterviewAPI,EvaluationAPI,FeedbackAPI,PaymentAPI,NewsAPI,NotificationAPI,WebClient}/Dockerfile.dockerignore` | CREATE — 12 files, identical contents |
| `.dockerignore` (root) | **DO NOT EDIT** — ai-service depends on it as-is |

Contents (note: **no `source/` line**, and paths are now repo-root-relative):
```
.git/
.github/
.claude/
.venv/
ai-service/
infra/
docs/
**/bin/
**/obj/
**/.vs/
**/node_modules/
**/*.user
**/appsettings.*.local.json
.env
.env.example
**/*.md
```

**Commit:** `chore(docker): add per-Dockerfile ignore files for .NET service images`
**Verify:** `find source -name "Dockerfile.dockerignore" | wc -l` → `12`; and confirm `grep -c "^source/$" .dockerignore` still returns `1` (root file unchanged).
**Requires BuildKit** (default in Docker Desktop 4.x and Compose v2). Confirm once: `DOCKER_BUILDKIT=1 docker build --help >/dev/null && echo ok`.
**Local dev impact:** none.

---

### Batch B — Dockerfile per service (12 commits, one branch)
**Branch:** `chore/dockerize-dotnet-services` · **TIER 1** · **Blocked by:** Batch A · **Blocks:** Batch H

One commit per service so a broken image is bisectable. Suggested order — least-coupled first, so early failures are cheap:

| # | Path | Commit message |
|---|---|---|
| 1 | `source/NewsAPI/Dockerfile` | `chore(docker): add Dockerfile for NewsAPI` |
| 2 | `source/NotificationAPI/Dockerfile` | `chore(docker): add Dockerfile for NotificationAPI` |
| 3 | `source/QuestionAPI/Dockerfile` | `chore(docker): add Dockerfile for QuestionAPI` |
| 4 | `source/CvJdAPI/Dockerfile` | `chore(docker): add Dockerfile for CvJdAPI` |
| 5 | `source/FeedbackAPI/Dockerfile` | `chore(docker): add Dockerfile for FeedbackAPI` |
| 6 | `source/PaymentAPI/Dockerfile` | `chore(docker): add Dockerfile for PaymentAPI` |
| 7 | `source/AuthAPI/Dockerfile` | `chore(docker): add Dockerfile for AuthAPI` |
| 8 | `source/UserAPI/Dockerfile` | `chore(docker): add Dockerfile for UserAPI` |
| 9 | `source/EvaluationAPI/Dockerfile` | `chore(docker): add Dockerfile for EvaluationAPI` |
| 10 | `source/InterviewAPI/Dockerfile` | `chore(docker): add Dockerfile for InterviewAPI` |
| 11 | `source/ApiGateway/Dockerfile` | `chore(docker): add Dockerfile for ApiGateway` |
| 12 | `source/WebClient/Dockerfile` | `chore(docker): add Dockerfile for WebClient` |

> ### Blocker B-1 — build context must be the repo root, not `source/`
> AuthAPI, UserAPI, and NotificationAPI each declare
> `<ProjectReference Include="..\..\contracts\auth\auth.csproj" />`
> (`AuthAPI.csproj:22`, `UserAPI.csproj:20`, `NotificationAPI.csproj:19`), which resolves to repo-root `contracts/auth/` — **outside `source/`**.
>
> **Docker cannot `COPY` from outside the build context.** The `COPY ../contracts/auth/auth.csproj` line in the plan's §1.2 template is invalid and would fail the build for those three services. Set the context to the repo root and make all paths repo-root-relative. Handled by Batch A's per-Dockerfile ignore files.
>
> Also confirmed: `DevPrep.sln` does **not** list the `auth` project (removed deliberately — see commit *"bo project 'auth' khoi DevPrep.sln (van build qua ProjectReference)"*). So `dotnet restore DevPrep.sln` will not restore it; restore the **individual csproj** so the ProjectReference is followed.

**Corrected template** (repo-root context; example InterviewAPI):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src
COPY source/DevPrep.sln source/
COPY source/InterviewAPI/InterviewAPI.csproj source/InterviewAPI/
# For AuthAPI / UserAPI / NotificationAPI ONLY, also:
# COPY contracts/auth/auth.csproj contracts/auth/
RUN dotnet restore source/InterviewAPI/InterviewAPI.csproj
COPY source/ source/
# COPY contracts/ contracts/       # same three services only
RUN dotnet publish source/InterviewAPI/InterviewAPI.csproj -c Release -o /app/publish --no-restore
```

> ### Blocker B-2 — `UseUrls()` overrides `ASPNETCORE_URLS`; the 10 backend services bind container-loopback
> All ten backend services call
> `builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}")`
> with `bindHost` defaulting to **`"localhost"`** and `servicePort` to 700x (`SERVICE_BIND_HOST` / `<X>_API_PORT`, e.g. `InterviewAPI/Program.cs:26-27`).
>
> An explicit `UseUrls()` call **takes precedence over the `ASPNETCORE_URLS` environment variable**, so the `ENV ASPNETCORE_URLS=http://0.0.0.0:8080` in the plan's template is **silently ignored** for these ten. The service binds `localhost:7005` *inside its own container* — reachable only from that container's loopback, so the gateway container gets connection-refused and the whole stack looks broken for no visible reason.
>
> **Fix — set both env vars per service in compose (the code already supports them):**
> ```yaml
> environment:
>   - SERVICE_BIND_HOST=0.0.0.0
>   - INTERVIEW_API_PORT=8080     # the service's own <X>_API_PORT key
> ```
> Keep `ENV ASPNETCORE_URLS` in the Dockerfile only for **WebClient**, which has no `UseUrls()` call and therefore does honour it.
>
> **ApiGateway is a partial exception:** it already binds `0.0.0.0` (`Program.cs:11`) but reads `GLOBAL_API_GATEWAY_PORT`, default **7000**. Set `GLOBAL_API_GATEWAY_PORT=8080` in compose, or publish `7000:7000` instead of `7000:8080`. Pick one and be consistent — this is a common source of a stack that starts but never answers.

> ### Blocker B-3 — ApiGateway and WebClient have no `/health`; the uniform HEALTHCHECK never passes
> Verified: `source/ApiGateway/Program.cs` has **no** `/health` endpoint (its only routes are the proxy), and `source/WebClient/Program.cs` has neither `UseUrls` nor `/health`. The other ten services all have one.
>
> A `HEALTHCHECK CMD curl -fsS http://localhost:8080/health` on those two containers fails forever → both stay `unhealthy` → **any `depends_on: { condition: service_healthy }` pointing at them never satisfies**, and `docker compose up` hangs or aborts.
>
> **Fix — add a trivial health endpoint to both** (2 lines each, additive, TIER 1, matches the existing convention in the other ten):
> ```csharp
> app.MapGet("/health", () => Results.Ok(new { service = "ApiGateway", status = "ok" }));
> ```
> For ApiGateway this must be registered **before** `app.MapReverseProxy()` so the proxy's catch-all doesn't swallow it.
> *(Alternative if you'd rather not touch their code: healthcheck WebClient on `/` and give ApiGateway a TCP-only check. Adding `/health` is cleaner and consistent.)*

**Verify per service:**
```bash
DOCKER_BUILDKIT=1 docker build -f source/<Svc>/Dockerfile -t devprep-<svc>:test .   # context = repo root
docker run --rm devprep-<svc>:test dotnet --info | head -3            # image runs
docker image inspect devprep-<svc>:test --format '{{.Config.User}}'   # must NOT be empty/root
```
**Verify all 12 at the end:** `docker images "devprep-*:test" --format '{{.Repository}}' | wc -l` → `12`
**Local dev impact:** none.

---

### Batch C — Externalize peer-service URLs (env var + current default)
**Branch:** `refactor/config-service-urls` · **TIER 1** · **Blocked by:** nothing · **Blocks:** Batch H

Rows 1–11 and 15–21 of plan §1.4. One commit per service.

| Path | Change | Commit |
|---|---|---|
| `source/ApiGateway/appsettings.json:343,350,360,367,374,381,388,395,402,409` | 10 cluster addresses → env-substitutable | `refactor(gateway): read cluster destinations from configuration` |
| `source/ApiGateway/Program.cs:36` | CORS default stays `http://localhost:5130` | *(same commit)* |
| `source/WebClient/appsettings.json:4` | gateway URL → env var, default unchanged | `refactor(webclient): read gateway base URL from configuration` |
| `source/InterviewAPI/appsettings.json:14` + `Program.cs:56` | ai-service URL → env var; **drop the `http://localhost:8000` literal fallback** | `refactor(interview): read ai-service base URL from configuration` |
| `source/EvaluationAPI/appsettings.json:14` + `Program.cs:40` | same | `refactor(evaluation): read ai-service base URL from configuration` |
| `source/QuestionAPI/appsettings.json:10` + `Program.cs:87` | same | `refactor(question): read ai-service base URL from configuration` |

**Verify:** `run-all.ps1` still starts everything; `curl http://localhost:7000/api/news` returns as before. Then:
`grep -rn "localhost:70" source --include="*.json" --include="*.cs" | grep -v /obj/ | grep -v /bin/` → only `.env`-driven defaults remain.
**Local dev impact:** none — defaults reproduce current values exactly.

---

### Batch D — UserAPI hardcoded peer URLs → configuration
**Branch:** `refactor/userapi-config-urls` · **TIER 1** *(logged as courtesy — `.cs` edit in a teammate service)* · **Blocked by:** nothing

| Path | Change |
|---|---|
| `source/UserAPI/Program.cs:49` | `new Uri("http://localhost:7001/")` → config `AuthApi:BaseUrl`, default `http://localhost:7001/` |
| `source/UserAPI/Program.cs:54` | `http://localhost:7005/` → config `InterviewApi:BaseUrl`, same default |
| `source/UserAPI/Program.cs:73` | `http://localhost:7008/` → config `PaymentApi:BaseUrl`, same default |

**Commit:** `refactor(user): read peer service URLs from configuration instead of hardcoding`
**Verify:** `grep -n "new Uri(\"http" source/UserAPI/Program.cs` → no matches. UserAPI still starts under `run-all.ps1`; admin user list still renders.
**Notify after:** Usopp204 *(see §0.2 of the plan — ownership on UserAPI is split; see ownership table in `team-notifications.md`)*
**Closes:** F-06.

---

### Batch E — CvJdAPI `AI_SERVICE_URL` port fix
**Branch:** `fix/cvjd-ai-service-url` · **TIER 1** · **Blocked by:** nothing

| Path | Change |
|---|---|
| `source/CvJdAPI/appsettings.json` | **ADD** `AI_SERVICE_URL` — see blocker E-1 below |
| `source/CvJdAPI/appsettings.Development.json:8` | `http://localhost:8000` → `http://localhost:8080` |
| `source/CvJdAPI/Endpoints/CvEndpoints.cs:48` | literal fallback → single config read |
| `source/CvJdAPI/Endpoints/JdEndpoints.cs:38` | same |
| `source/CvJdAPI/Endpoints/JdEndpoints.cs:96` | same |
| `source/CvJdAPI/Endpoints/MatchEndpoints.cs:30` | same |

> ### Blocker E-1 — `AI_SERVICE_URL` exists only in the Development settings file
> `AI_SERVICE_URL` is defined in `appsettings.Development.json:8` and **nowhere else** — `grep -n "AI_SERVICE_URL" source/CvJdAPI/appsettings.json` returns nothing.
>
> In a container, `ASPNETCORE_ENVIRONMENT` will not be `Development`, so that file is never loaded and the four call sites fall through to their hardcoded `http://localhost:8000` literal — the exact broken value this batch exists to fix. Fixing only the Development file would make the bug *invisible in local dev and still present in the container.*
>
> **Fix:** add `AI_SERVICE_URL` to `appsettings.json` as well (value `http://localhost:8080` for the non-container default), and supply the compose value via env var in Batch H.

**Commit:** `fix(cvjd): point AI_SERVICE_URL at the published ai-service port`
**Verify:** with ai-service running in Docker, upload a CV through the app — parsing succeeds (it currently fails with connection-refused). Then confirm the non-Development path: `ASPNETCORE_ENVIRONMENT=Production dotnet run` and check the resolved URL is 8080, not 8000.
**Notify after:** Duong. **Closes:** plan §0(c) — a live bug, not just prep.

---

### Batch F — Remove the `.env.example` runtime load (F-12)
**Branch:** `fix/remove-env-example-runtime-load` · **TIER 1** · **Blocked by:** the pre-check below

> ### ✅ Pre-check CLEARED — the ElevenLabs concern was a false alarm
> The earlier blocking pre-check on this batch has been **withdrawn**. It rested on a wrong assumption: that the three `.env.example`-only keys (`ELEVENLABS_API_KEY`, `ELEVENLABS_VOICE_ID`, `ELEVENLABS_TTS_MODEL`) were consumed by the .NET services this batch touches. They are not.
>
> Verified:
> - `grep -rni "elevenlabs" source --include="*.cs" --include="*.json"` → **zero hits**. No .NET service reads these keys.
> - They are read only by ai-service: `ai-service/app/core/config.py:58-60`.
> - ai-service loads env via `load_dotenv(ROOT_ENV_PATH)` (`config.py:8,12`) — it reads the **root `.env` only** and has never loaded `.env.example`.
>
> This batch removes `Env.TraversePath().Load(".env.example")` from **.NET** `Program.cs` files, which sets env vars in .NET processes only. Python is a separate process. **Batch F therefore cannot affect ElevenLabs, or ai-service at all.** Proceed without preconditions.
>
> *(The ElevenLabs keep-or-drop question is a real decision, but it is independent of this batch and not blocking. See the analysis handed over separately.)*

| Paths | Change |
|---|---|
| 11 × `source/*/Program.cs` (`ApiGateway:4-5`, `AuthAPI:16-17`, `UserAPI:16-17`, `CvJdAPI:6-7`, `QuestionAPI:13-14`, `InterviewAPI:16-17`, `EvaluationAPI:15-16`, `FeedbackAPI:16-17`, `PaymentAPI:17-18`, `NewsAPI:13-14`, `NotificationAPI:12-13`) | Delete `try { Env.TraversePath().Load(".env.example"); } catch { }` |

**Commit:** `fix(config): stop loading .env.example at runtime`
**Verify:** `grep -rn 'Load(".env.example")' source --include="*.cs" | grep -v /obj/` → no matches. Then full `run-all.ps1` start and one end-to-end interview.
**Local dev impact:** none **after** the pre-check is handled.

---

### Batch G — Delete the orphaned gateway Dockerfile (F-11)
**Branch:** `chore/remove-orphaned-dockerfile` · **TIER 1** · **Blocked by:** Batch B item 11

| Path | Action |
|---|---|
| `source/Dockerfile` | DELETE — builds only ApiGateway, hardcodes port 5000 vs the code's 7000, never referenced by compose |

**Commit:** `chore(docker): remove orphaned root Dockerfile superseded by per-service images`
**Verify:** `test -f source/Dockerfile && echo STILL THERE || echo removed`
**Do not run before** Batch B item 11 lands, or the gateway has no image at all.

---

### Batch H — Compose skeleton + .NET services (boot-level only)
**Branch:** `chore/compose-dotnet-services` · **TIER 1** · **Blocked by:** Batches B, C, D, E

Adds every .NET service to compose and proves containers **start and answer `/health`**. Real request flows stay unverifiable until schema lands — that check is in the blocked section.

| Path | Change |
|---|---|
| `infra/docker-compose.yml` | Add 12 .NET services: **`build: { context: .., dockerfile: source/<Svc>/Dockerfile }`** (repo root — blocker B-1), `networks: [devprep-internal-net]`, env vars per below, **no `ports:` except apigateway and webclient** |
| `infra/docker-compose.override.yml` | CREATE — dev-only port publishing (`sqlserver:1433`, `rabbitmq:15672`); never in the base file |
| `infra/.env.docker.example` | CREATE — container-side values template |

**Mandatory env vars per backend service (blocker B-2) — without these the stack starts but nothing can reach anything:**
```yaml
environment:
  - SERVICE_BIND_HOST=0.0.0.0        # else UseUrls() binds container-loopback
  - <SVC>_API_PORT=8080              # AUTH_API_PORT, USER_API_PORT, INTERVIEW_API_PORT, ...
```
ApiGateway instead takes `GLOBAL_API_GATEWAY_PORT=8080` (it already binds `0.0.0.0`). WebClient needs neither — it has no `UseUrls()` and honours `ASPNETCORE_URLS` from the Dockerfile.

**Prerequisite from blocker B-3:** ApiGateway and WebClient must have a `/health` endpoint before their containers can ever report healthy. Land that first or their `depends_on` gates will never open.

**Commits (one per service):** `chore(compose): add <Service> to the compose stack`

**Verify:**
```bash
cd infra && docker compose config --quiet          # schema of the compose file is valid
docker compose up -d apigateway webclient
docker compose ps                                   # both healthy
curl -fsS http://localhost:7000/                    # gateway answers
# lockdown gate — MUST all fail:
for p in 7001 7002 7003 7004 7005 7006 7007 7008 7009 7010; do
  curl -s -o /dev/null -m 2 "http://localhost:$p/health" && echo "LEAK on $p" || echo "ok $p refused"
done
```
Any `LEAK` line fails the batch — see plan §1.5, mandatory lockdown.
**Local dev impact:** none — compose is additive; `run-all.ps1` untouched.

---

### Batch I — Env restructuring and the strong SQL password
**Branch:** `chore/env-split-docker-local` · **TIER 1** · **Blocked by:** Batch H

> **This is the batch that can break local dev.** The rule: container values go in `infra/.env.docker`; the root `.env` keeps pointing at `localhost\MSSQLSERVER01` with its current credentials. **Do not edit `.env:1-11`.**

| Path | Change |
|---|---|
| `infra/.env.docker` (gitignored) | CREATE — `MSSQL_SA_PASSWORD` (≥8 chars, complexity — the SQL Server image refuses `12345` and exits), all 10 `*_DB_CONNECTION_STRING` values pointing at `Server=sqlserver,1433`, `INTERNAL_API_KEY`, `JWT_KEY` |
| `infra/.env.docker.example` | Committed template with placeholders |
| `.gitignore` | Add `infra/.env.docker` |
| `.env:7` | DELETE `STATISTICS_DB_CONNECTION_STRING` — service removed by PR #78 |
| `.env.example` | Mirror the deletion |

**Commit:** `chore(config): split container env from local env and drop dead Statistics connection string`
**Verify:** `run-all.ps1` still starts everything and an interview completes end-to-end. `grep -n "MSSQLSERVER01" .env | wc -l` → still `10` (was 11; Statistics removed).

---

### Batch J — StatisticsAPI residue cleanup
**Branch:** `chore/remove-statisticsapi-residue` · **TIER 1** · **Blocked by:** nothing

Already removed from `dev` by PR #78 four days ago; only untracked `bin/`/`obj/` remains on local disks.

| Path | Action |
|---|---|
| `source/StatisticsAPI/` | DELETE (local filesystem only — untracked, **zero git diff**) |
| `docs/architecture/pure-microservices-baseline.md:39` | Remove the `/api/statistics/*` row |
| `contracts/README.md:31` | Remove Statistics from the boundary list |

**Commit:** `docs: remove StatisticsAPI from architecture docs after its removal in PR #78`
**Verify:** `test -d source/StatisticsAPI && echo STILL THERE || echo removed`; `grep -rin statistics docs/architecture contracts/README.md` → no stale service references.
**Note:** the directory delete produces no commit — it is untracked residue. Only the two doc edits are committed.

---

## Schema-blocked work — cannot complete until §1.1 is approved

Everything below depends on committed baseline schema (`docs/architecture/schema-recovery.md`) and the removal of `.gitignore:106`. **All are TIER 3-gated.** Write the artifacts now; they just cannot be *verified* until approval lands.

### Batch K — SQL Server + db-init containers
**Branch:** `chore/compose-sqlserver` · **TIER 3 to complete, TIER 1 to draft** · **Blocked by:** §1.1 approval

| Path | Change | Can draft now? |
|---|---|---|
| `infra/docker-compose.yml` | `sqlserver` service (image, healthcheck, `mssql-data` volume) | **Yes** — a SQL Server container runs fine with no databases |
| `db/init.sh` | CREATE — create catalogs, apply baselines, then `source/*/Sql/*.sql` in lexical order | **Yes** — write the script; it just has nothing to apply yet |
| `infra/docker-compose.yml` | `db-init` one-shot service | **Yes** |
| `db/baseline/000_*.sql` | The 10 baseline scripts | **No — blocked** |
| `.gitignore:106` | Remove `/source/*/Sql/` | **No — blocked (TIER 3)** |

**Partial verify available now:** `docker compose up -d sqlserver && docker compose ps` → healthy. Confirms image tag, password complexity, and volume before approval lands.
**Full verify (blocked):** `docker compose run --rm db-init` exits `0` and `sqlcmd -Q "SELECT name FROM sys.databases"` lists all 10.

### Batch L — Full-stack acceptance
**Blocked by:** Batch K complete

| Check |
|---|
| `docker compose up -d` on a clean clone → 15 services, all healthy, `db-init` `exited (0)` |
| Browser → `localhost:5130` → log in → start interview → answer a question |
| `docker compose exec apigateway curl -fsS http://interviewapi:8080/health` → 200 |
| Lockdown gate (Batch H command) still all-refused |

### Batch M — CvJdAPI schema trap
**Blocked by:** Batch K · **Must be resolved before the stack boots correctly**

> `source/CvJdAPI/Program.cs:68` calls `db.Database.EnsureCreated()`. **`EnsureCreated()` does nothing if the database already exists** — it does not create missing tables in an existing database. So if `db-init` creates an empty `DevPrep_CvJd`, CvJdAPI will start, find the database present, skip table creation, and fail on first query.
>
> **Two valid resolutions — pick one and write it down:**
> (a) `db-init` creates `DevPrep_CvJd` **with its full schema** (script it like the other nine), or
> (b) `db-init` **skips** `DevPrep_CvJd` entirely and lets `EnsureCreated()` own it.
>
> (a) is recommended for consistency and determinism. (b) is fewer moving parts but leaves one database whose schema lives only in the EF model. Detail is in `schema-recovery.md` §4.

---

## Batch dependency graph

```
A ──> B ──> G
      │
      ├──> H ──> I
C ────┤
D ────┤
E ────┘
F  (independent, after its pre-check)
J  (independent)

      ══ TIER 3 approval ══
                │
                ▼
          K ──> L
          K ──> M
```

**Critical path to a demoable stack:** A → B → H → K → L. Batches C, D, E must land before H. F, J, G are independent and can fill waiting time.
