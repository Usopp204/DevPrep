# Phase 1 — Containerization Execution Checklist

**Companion to:** `docs/architecture/microservice-remediation-plan.md` (Phase 1) and `docs/architecture/schema-recovery.md` (the §1.1 TIER 3 item).
**Design goal:** local development keeps working at **every** commit boundary. See §0 for how that is guaranteed and the one task that would break it if done wrong.

> ### ⚠ Local dev is MANUAL PowerShell startup, not `run-all.ps1`
> `run-all.ps1` exists in the repo but is **not the workflow in use**. Services are started by hand. Every verification step below is written for manual startup — see **§0.1** for the exact command and the three things `run-all.ps1` did silently that you must now do yourself.
>
> Earlier revisions of this checklist said "verify with `run-all.ps1`". Those steps were unrunnable and were being skipped, which means changes were landing unverified. They have all been rewritten.
**Resumable:** batches are independent and ordered by dependency. Stop after any batch; the tree is always in a working state.

---

## 0 — The rule that keeps local dev alive

**Never repoint the root `.env` at the container.** Two env files, two modes:

| File | Used by | DB target | Committed? |
|---|---|---|---|
| `.env` (root) | manual startup, `dotnet run`, venv ai-service | `localhost\MSSQLSERVER01` — **unchanged throughout Phase 1** | No (gitignored) |
| `infra/.env.docker` (**new**) | `docker compose` only | `sqlserver,1433` | No — but `infra/.env.docker.example` is |

Every config change in this checklist follows the same shape: **read from an env var, default to today's literal value.** Container mode overrides via compose; local mode falls through to the default and behaves exactly as it does now.

> **The single task that breaks local dev if done wrong: Batch I.** If the root `.env` connection strings are edited to point at `sqlserver,1433` or the sa password is changed there, manual startup stops working immediately. Batch I is written to avoid this — container values go in `infra/.env.docker`, never the root `.env`. If you find yourself editing `.env:1-11`, stop.

---

## 0.1 — Manual startup: the command, and what `run-all.ps1` was doing for you

### The command

```powershell
cd Q:\DevPrep\source\<Project>          # MANDATORY — see (1) below
$env:ASPNETCORE_ENVIRONMENT = 'Development'   # MANDATORY — see (2) below
dotnet bin\Debug\net8.0\<Project>.dll --urls http://localhost:<port>
```

Ports: ApiGateway 7000, Auth 7001, User 7002, CvJd 7003, Question 7004, Interview 7005, Evaluation 7006, Feedback 7007, Payment 7008, News 7009, Notification 7010, WebClient 5130.

### The three things `run-all.ps1` did that manual startup does not

**(1) `cd` into the project directory (`run-all.ps1:72`) — the most damaging one to omit.**
ASP.NET Core's **content root is the current working directory**, not the DLL's location. `appsettings.json` is resolved from the content root. Run the DLL from the repo root and **`appsettings.json` is never loaded** — JWT keys, `AiService:BaseUrl`, `AI_SERVICE_URL`, and the YARP `ReverseProxy` section all vanish, and the service fails with confusing "not configured" errors.
DotNetEnv's `TraversePath()` walks *up* from the working directory, so it still finds the repo-root `.env` from either location — which is why the failure looks partial and puzzling: env-var config works, `appsettings.json` config doesn't.

**(2) `$env:ASPNETCORE_ENVIRONMENT = 'Development'` (`run-all.ps1:74`).**
Running a DLL directly **ignores `launchSettings.json`** (the script says so at `:28-29`), so the environment is unset and .NET defaults to **Production**. Consequences:
- `appsettings.Development.json` does not load.
- Swagger disappears (all 11 services gate it behind `IsDevelopment()`).
- **`source/ApiGateway/appsettings.Production.json` DOES load** — the file Batch C added, containing compose DNS names. **The gateway will try to route to `http://authapi:8080/`, which does not resolve on the host, and every route fails.** This is the single most likely confusing failure of manual startup, and it is a direct consequence of a Batch C change.

**(3) `--urls http://localhost:<port>` (`run-all.ps1:79`).**
Nine services compute their own URL from `SERVICE_BIND_HOST`/`<SVC>_API_PORT` and would bind correctly anyway. But **AuthAPI and WebClient have no `UseUrls()` call** — with no `--urls` and no `launchSettings.json`, they fall back to Kestrel's default **`http://localhost:5000`**, not 7001/5130. Always pass `--urls`.

### Config load order at manual startup

1. `Env.TraversePath().Load()` runs *before* `WebApplication.CreateBuilder`, setting process env vars from the repo-root `.env` (found by walking up from the working directory).
2. `appsettings.json` — from the content root (= working directory).
3. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` — `Development` if you set it, otherwise `Production`.
4. Environment variables — including everything `.env` just set. **Overrides 2 and 3.**
5. Command-line args (`--urls`). **Highest precedence.**

### WDAC: does the launch method change which sources load? **Yes.**

| Launch | Content root | `launchSettings.json` | Environment | Port |
|---|---|---|---|---|
| `dotnet run --project …` (InterviewAPI etc.) | project dir, automatic | **read** | `Development`, automatic | from `applicationUrl`, automatic |
| `dotnet bin\Debug\net8.0\X.dll` (EvaluationAPI, WebClient — WDAC-forced) | **your cwd** | **ignored** | **Production** unless you set it | Kestrel default unless you pass `--urls` |

So the two WDAC-blocked services are exactly the ones that need all three manual steps, while `dotnet run` gives them for free. Mixing the two launch styles across services in one session is the likeliest source of "it works for Interview but not Evaluation".

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

**Verify (manual startup):** start NewsAPI and ApiGateway, each in its own PowerShell window:
```powershell
cd Q:\DevPrep\source\NewsAPI
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet bin\Debug\net8.0\NewsAPI.dll --urls http://localhost:7009

cd Q:\DevPrep\source\ApiGateway
$env:ASPNETCORE_ENVIRONMENT = 'Development'   # WITHOUT this the gateway loads appsettings.Production.json and routes to compose DNS names
dotnet bin\Debug\net8.0\ApiGateway.dll --urls http://localhost:7000
```
Then `curl http://localhost:7000/api/news` returns as before. Also:
`grep -rn "localhost:70" source --include="*.json" --include="*.cs" | grep -v /obj/ | grep -v /bin/` → only config defaults remain.
**Local dev impact:** none — defaults reproduce current values exactly.

---

### Batch D — UserAPI hardcoded peer URLs → configuration
**Branch:** `refactor/userapi-config-urls` · **TIER 1** *(logged as courtesy — `.cs` edit in a teammate service)* · **Blocked by:** nothing

> ### Corrected during execution — use the repo's existing key names
> This batch originally specified `AuthApi:BaseUrl` / `InterviewApi:BaseUrl` / `PaymentApi:BaseUrl`. **That was wrong.** The repo already has an established convention for peer-service URLs, used by InterviewAPI (`Program.cs:54-63`), PaymentAPI (`:94`), and FeedbackAPI (`:45-46`):
>
> `AUTH_API_BASEURL`, `INTERVIEW_API_BASEURL`, `PAYMENT_API_BASEURL`
>
> Introducing a second scheme for the same concept would have forced compose to set **two different variable names for the same URL** — `AUTH_API_BASEURL` for InterviewAPI/PaymentAPI/FeedbackAPI and `AuthApi__BaseUrl` for UserAPI — a latent Batch H bug with no detection until a route failed at runtime. The established names are used instead.

| Path | Change |
|---|---|
| `source/UserAPI/Program.cs:49` | `new Uri("http://localhost:7001/")` → config `AUTH_API_BASEURL`, default `http://localhost:7001/` |
| `source/UserAPI/Program.cs:54` | `http://localhost:7005/` → config `INTERVIEW_API_BASEURL`, same default |
| `source/UserAPI/Program.cs:73` | `http://localhost:7008/` → config `PAYMENT_API_BASEURL`, same default |
| `source/UserAPI/Program.cs:46` | Stale comment (*"gọi trực tiếp không thông qua file config/JSON"*) rewritten — it described the anti-pattern being removed |

Trailing slashes are preserved exactly as they were, so `HttpClient.BaseAddress` relative-URL resolution is unchanged. This batch is behaviour-neutral in local dev.

**Commit:** `refactor(user): read peer service URLs from configuration instead of hardcoding`
**Verify (manual startup):** `grep -n "new Uri(\"http" source/UserAPI/Program.cs` → no matches. Then:
```powershell
cd Q:\DevPrep\source\UserAPI
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet bin\Debug\net8.0\UserAPI.dll --urls http://localhost:7002
```
Expect "Now listening on: http://localhost:7002". Then start PaymentAPI (7008) the same way and load the admin user list through WebClient — the last-activity column exercises `PaymentServiceClient`, the one client that also carries the internal API key.
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

> **Follow-up recorded during execution, not a task.** The four call sites now raise a clear error instead of falling back to a hardcoded URL — but because `AI_SERVICE_URL` is read *inside the endpoint handlers*, that error surfaces per-request as a 500 rather than refusing startup the way Batch C's equivalents do. Making it fail fast would mean reading the value once in `Program.cs` and passing it into `MapCvEndpoints`/`MapJdEndpoints`/`MapMatchEndpoints` — a change to how CvJdAPI's endpoints take configuration, which is teammate-owned structure and out of proportion for an infrastructure batch. Left as-is deliberately.

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
**Verify (manual startup):** `grep -rn 'Load(".env.example")' source --include="*.cs" | grep -v /obj/` → no matches.

Then the highest-value check in Phase 1, because it exercises the whole config chain the removed load touched — start the full stack manually and run one end-to-end interview:
```powershell
# one window per service; ALL need the cd + ASPNETCORE_ENVIRONMENT + --urls
# order matters only loosely: backends, then gateway, then WebClient
# 7001 Auth, 7002 User, 7003 CvJd, 7004 Question, 7005 Interview,
# 7006 Evaluation, 7007 Feedback, 7008 Payment, 7009 News, 7010 Notification,
# 7000 ApiGateway, 5130 WebClient
```
Watch for two specific failures this batch could cause: any service reporting a missing config key at startup (a key that was silently resolving from `.env.example`), and the gateway 502-ing every route (that means `ASPNETCORE_ENVIRONMENT` was not set and `appsettings.Production.json` loaded).
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

> ### H-1 — `ASPNETCORE_ENVIRONMENT` is per-service, and only the gateway gets `Production`
> Batch C added `source/ApiGateway/appsettings.Production.json` holding the ten compose-DNS cluster addresses. It loads only under `ASPNETCORE_ENVIRONMENT=Production`.
>
> **Set `Production` on the `apigateway` container only. Leave every other service on `Development`.**
>
> Verified rationale: `ApiGateway/Program.cs` contains **zero** `IsDevelopment()` branches — no Swagger, no developer exception page — so Production costs it nothing. Every one of the other eleven services has exactly one `IsDevelopment()` branch (Swagger UI for the APIs, the dev exception page for WebClient). Putting them in Production would silently remove both, which is precisely the debugging surface Batch H needs.
>
> The other services don't need Production anyway: their overridable keys are flat or single-nested (`AiService__BaseUrl`, `ApiGateway__BaseUrl`, `USER_API_BASEURL`) and set cleanly as env vars with no hyphens. The gateway's nested, hyphenated cluster keys were the only awkward case, and the JSON file resolves it.
>
> ```yaml
> apigateway:
>   environment:
>     - ASPNETCORE_ENVIRONMENT=Production     # gateway ONLY
> interviewapi:
>   environment:
>     - ASPNETCORE_ENVIRONMENT=Development    # keep Swagger for debugging
>     - AiService__BaseUrl=http://ai-service:8000
> ```
> Mixed environments across containers is unusual but valid, and deliberate here. Revisit before a production deployment; for the demo it is the right trade.

> ### H-2 — WebClient defines its listen URL twice
> `source/WebClient/appsettings.json:2` sets `"Urls": "http://localhost:5130"`, **in addition to** the `ApiGateway:BaseUrl` entry at `:4` that Batch C reviewed. Two different keys, two different purposes: `Urls` is where WebClient *listens*, `ApiGateway:BaseUrl` is what it *calls*.
>
> The container is fine — `source/WebClient/Dockerfile` sets `ASPNETCORE_URLS=http://0.0.0.0:8080`, and the environment-variable provider is registered after the JSON provider, so the env var wins and WebClient binds `0.0.0.0:8080` as intended.
>
> Recorded because it is genuinely non-obvious: a future reader debugging a WebClient port problem will find `"Urls": "http://localhost:5130"` in appsettings.json, assume it is authoritative, and lose time. If that binding ever *does* look wrong in a container, the cause is provider ordering, not this line.

> ### H-3 — Avatar files are served outside the gateway and break under port lockdown
> **Two changes needed in Batch H. No code change, and — verified below — no data migration.**
>
> **The problem.** `UserAPI/Services/LocalFileStorageService.cs:45` builds an **absolute** avatar URL from `FILE_PUBLIC_BASE_URL` (default `http://localhost:7002`, line 12), and `UserAPI/Program.cs:153` serves those files via `app.UseStaticFiles()` from `wwwroot/avatars/`. The browser fetches them **directly from UserAPI's port, bypassing the gateway** — verified: `grep -i "avatar\|static\|upload"` over `ApiGateway/appsettings.json` returns nothing, and `users-route` only matches `/api/users/{**catch-all}`.
>
> Under the mandatory port lockdown (§1.5) UserAPI publishes no host port, so `http://localhost:7002` is unreachable from the browser — and `http://userapi:8080` is compose-internal, so it is *also* unreachable from a browser. **In compose there is no valid value that points at UserAPI directly.** The URL has to go through the gateway.
>
> **Fix, part 1 — new gateway route (SHARED, additive).** Add to `source/ApiGateway/appsettings.json`:
> ```json
> "avatars-route": {
>   "ClusterId": "users-cluster",
>   "Match": { "Path": "/avatars/{**catch-all}" }
> }
> ```
> No `PathRemovePrefix` — UserAPI serves these at `/avatars/...` and expects the path intact. Additive only; it adds a route that does not exist today and changes no existing one. The matching entry must also go into `appsettings.Production.json`? **No** — routes are inherited from `appsettings.json` and only cluster *addresses* are overridden there (see the `_comment` in that file).
>
> **Fix, part 2 — env var for the UserAPI container.** `FILE_PUBLIC_BASE_URL=http://localhost:7000` (the gateway's published port, i.e. what the *browser* can reach). Note this is a browser-facing URL, so it takes the host address — the same trap as VNPay's `ReturnUrl` at `.env:63`, and the opposite of every other compose URL in §1.4. `LocalFileStorageService.cs:25` already reads this key from configuration, so **no code change is required**; the const at line 12 stays as the correct local-dev default.
>
> ### Verified: no data migration needed
> The concern was that existing `UserProfiles.AvatarUrl` rows hold absolute `http://localhost:7002/avatars/…` strings that config cannot retroactively fix. **That does not apply to compose**, because the compose database starts empty of user data:
> - No code seeds `UserProfiles` — the only insert is `UserProfileRepository.cs:42`, reached at runtime via `CreateProfileIfNotExistsAsync` when an account registers.
> - No committed SQL script contains an `INSERT` or the word `avatar`.
> - `schema-recovery.md` §3.1 seeds only `DevPrep_Question` (tech stacks, positions), `DevPrep_Payment` (credit packages), and `DevPrep_Notification` (email templates), and explicitly excludes user rows as personal data.
>
> So on a fresh volume there are zero profiles and therefore zero avatar URLs; accounts are registered fresh during the demo and every upload gets the correct base URL from the env var.
>
> **Stale absolute URLs exist only in individual developers' local databases.** That is a local annoyance, not a demo risk, and is not worth fixing here.
>
> **Consequently the two heavier options were considered and rejected:** a one-time `UPDATE` over `UserProfiles.AvatarUrl` (unnecessary — no rows), and switching to relative paths composed at render time (unnecessary, and it would touch every avatar read site across teammate-owned WebClient views with a real chance of missing one). If local-dev avatars ever need fixing, a developer can re-upload or run the `UPDATE` on their own machine.

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
**Verify (manual startup):** `grep -n "MSSQLSERVER01" .env | wc -l` → still `10` (was 11; Statistics removed). Then start any two services manually (per §0.1) and confirm both connect to `localhost\MSSQLSERVER01` as before — if either now tries `sqlserver,1433`, the root `.env` was edited and must be reverted.

> **Batch I note — `.env` is gitignored.** The ElevenLabs addition (option (ii): add `ELEVENLABS_API_KEY`; `ELEVENLABS_VOICE_ID` and `ELEVENLABS_TTS_MODEL` already match the code defaults at `ai-service/app/core/config.py:59-60`, so they are optional) is a **local-machine edit that never reaches the repo**. Do not expect it in a diff, a PR, or a teammate's clone — anyone else wanting the ElevenLabs fallback must add the key to their own `.env`. Only `.env.example` is tracked.

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
