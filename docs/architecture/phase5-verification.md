# Phase 5 — One-Pass Verification Checklist

**What this is.** A single run-through that exercises everything built in Phase 5 (resilience,
honest failure handling, grading recovery), plus the demo session and sample-data seeding. Most of
these were built without being verified individually, so this is the pass that catches whatever was
never actually run.

**How to read each item.** Every item states what it proves, whether it needs a rebuild, whether it
destroys state, the exact commands, and what counts as PASS versus FAIL. No item assumes you
remember how the feature works internally — the mechanism is explained where it matters.

**Ground rules while running this.**
- Run items in the order given. The order is chosen so an early failure does not invalidate later
  results — infrastructure first, read-only checks next, failure injection after that, and the one
  destructive item last.
- `docker pause` / `docker unpause` is the standard way to simulate a dead dependency here. It
  freezes the process without closing its listening socket, so callers see a hang rather than an
  instant connection refusal — which is what a real degraded service looks like.
- Container names: `devprep-gateway`, `devprep-webclient`, `devprep-interview`, `devprep-evaluation`,
  `devprep-payment`, `devprep-cvjd`, `devprep-news`, `devprep-ai-node`, `devprep-sqlserver`.
- All compose commands run from `Q:\DevPrep\infra` and need `--env-file .env.docker`.

---

## 0 — Prerequisites

### 0.1 Rebuild required before starting

| Item | Needs rebuild of | Why |
|---|---|---|
| 2 (gateway timeouts) | `apigateway` | `Program.cs` and `appsettings.json` changed |
| 6 (honest failures), 7 (breakers) | `interviewapi`, `evaluationapi` | Polly policies and failure handling |
| 8 (demo session) | `interviewapi`, `webclient` | Flag is read at startup |
| CV/JD screens in item 6 | `webclient` | Timeout split and `CvJdService` changes |

```powershell
cd Q:\DevPrep\infra
docker compose --env-file .env.docker -f docker-compose.yml up -d --build apigateway webclient interviewapi evaluationapi
docker compose --env-file .env.docker -f docker-compose.yml ps
```

**PASS:** all listed services `(healthy)`.
**FAIL:** any service `(unhealthy)` or restarting — stop here and read
`docker logs <container>`. Nothing below is meaningful against a broken stack.

### 0.2 Which items destroy state

| Item | Destroys state? | Repeatable on the same account? |
|---|---|---|
| 1, 2, 3, 5 | No | Yes |
| 6, 7 | No — `unpause` restores everything | Yes |
| 4 (out of credits) | Consumes credits if it succeeds | Use a **fresh** account |
| 8 (demo session) | Creates sessions, spends 6 credits each | Yes, while credits last |
| **9 (regrade recovery)** | **Yes — permanently** | **No. One fresh session per test.** |

Item 9 kills grading jobs mid-flight. A session whose grading was lost stays marked that way in
`AnswerEvaluations` even after recovery, and re-running the same test against the same session will
not reproduce the starting condition. **Each of tests A, B, C, D in item 9 needs its own new
session.** Budget four sessions (24 credits at the demo price, 120 at the 15-minute price).

---

## 1 — Port lockdown still holds

**Proves:** no backend service became reachable from the host during any of the recent changes.
Cheapest possible check, and it fails loudly if the compose file drifted.
**Rebuild:** no. **Destroys state:** no.

```powershell
foreach ($p in 7001,7002,7003,7004,7005,7006,7007,7008,7009,7010) {
  $r = try { (Invoke-WebRequest "http://localhost:$p/health" -TimeoutSec 2 -UseBasicParsing).StatusCode } catch { "refused" }
  "port $p -> $r"
}
"gateway  -> " + (Invoke-WebRequest "http://localhost:7000/health" -TimeoutSec 5 -UseBasicParsing).StatusCode
"webclient-> " + (Invoke-WebRequest "http://localhost:5130/health" -TimeoutSec 5 -UseBasicParsing).StatusCode
```

**PASS:** all ten backend ports print `refused`; gateway and webclient print `200`.
**FAIL:** any backend port returns a status code. That is a published port that should not exist —
check for a stray `ports:` block in `docker-compose.yml`. This is a review-blocking defect, not a
warning: centralising auth at the gateway is meaningless if services answer directly.

---

## 2 — Gateway timeout scoping

**Proves:** the per-route timeout policies added in Batch 5.3 actually select by route, rather than
one blanket timeout being raised for everyone.
**Rebuild:** `apigateway`. **Destroys state:** no.

**Mechanism, briefly.** `ApiGateway/Program.cs` registers three named policies — the default (30s),
`ai-backed` (90s) and `document` (330s). Routes pick one by name via `"TimeoutPolicy"` in
`appsettings.json`. Before this change a single 30s default applied to every proxied request, so CV
parsing died at 30s no matter what the document cluster allowed.

### 2a — Document route must now run well past 30 seconds

```powershell
docker pause devprep-cvjd
$sw=[Diagnostics.Stopwatch]::StartNew()
try { curl.exe -s -o NUL -w "http_code=%{http_code} time_total=%{time_total}" --max-time 60 http://localhost:7000/api/document/health }
finally { $sw.Stop(); docker unpause devprep-cvjd | Out-Null }
"elapsed_sec = $([math]::Round($sw.Elapsed.TotalSeconds,1))"
```

**PASS:** runs the full 60s and `curl` exits 28 (its own `--max-time`). Measured: **60.1s**.
**FAIL:** stops near 30s. The policy is not being applied — check that `"TimeoutPolicy": "document"`
is on `document-route` and the name matches `Program.cs` exactly.

Do not wait the full 330s; 60s already disproves the 30s cap.

### 2b — Default route must still cut at 30 seconds

```powershell
docker pause devprep-news
$sw=[Diagnostics.Stopwatch]::StartNew()
try { curl.exe -s -o NUL -w "http_code=%{http_code} time_total=%{time_total}" --max-time 50 http://localhost:7000/api/news }
finally { $sw.Stop(); docker unpause devprep-news | Out-Null }
"elapsed_sec = $([math]::Round($sw.Elapsed.TotalSeconds,1))"
```

**PASS:** ~30s. Measured: **30.1s**.
**FAIL:** runs to 50s — the default policy was raised, meaning DB-only routes now hold connections
for far longer than they should.

**Both together are the test.** 2a alone would also pass if someone simply raised the global
timeout; only the pair proves per-route scoping.

**Known and not fixed:** the timeout surfaces to the client as HTTP **400**, not 504. YARP is
awaiting the downstream on `HttpContext.RequestAborted` and cannot distinguish "an outer middleware
timed me out" from "the client disconnected", so it writes `RequestCanceled` (400) before the
timeout middleware can write 504. Do not record this as a failure of item 2 — it is a separate,
documented issue with the fix described in `ApiGateway/Program.cs`.

---

## 3 — Seeded accounts and credit packages

**Proves:** `SEED_SAMPLE_DATA` produced a usable system. Everything after this item needs a login,
so failures here block the rest.
**Rebuild:** no. **Destroys state:** no.

**Password for all three accounts: `DevPrep@Local1`** (documented publicly on purpose in
`db/seed/README.md` — these are local-only accounts).

| Email | Role | Expected |
|---|---|---|
| `admin@local.devprep.test` | Admin | reaches the admin area |
| `manager@local.devprep.test` | Manager | reaches the manager area |
| `candidate@local.devprep.test` | User | 10,000 credits, no admin access |

First confirm the data landed:

```powershell
docker exec devprep-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT (SELECT COUNT(*) FROM DevPrep_Auth.dbo.Accounts) AS accounts, (SELECT COUNT(*) FROM DevPrep_User.dbo.UserProfiles) AS profiles, (SELECT COUNT(*) FROM DevPrep_Payment.dbo.CreditPackages) AS packages, (SELECT SUM(CreditChange) FROM DevPrep_Payment.dbo.CreditLedger) AS candidate_balance"'
```

**PASS:** `accounts=3 profiles=3 packages=3 candidate_balance=10000`.
**FAIL — `candidate_balance` is NULL or 0:** the ledger row never landed. Re-run `db-init`; the seed
marker design means a failed seed leaves no marker and retries cleanly.

Then in the browser at `http://localhost:5130`:

1. Sign in as each of the three. **PASS:** all three succeed. **FAIL:** "invalid credentials" means
   the BCrypt hash in `01_DevPrep_Auth.sql` does not match `DevPrep@Local1` — regenerate it per
   `db/seed/README.md`.
2. As admin and as manager, open their admin areas. **PASS:** both load. **FAIL:** redirected to the
   candidate home — the seeded `Role` value is wrong.
3. As candidate, open the profile/navbar credit display. **PASS:** shows `10,000`. **FAIL:** shows
   `0` or `—` — see item 6, this may be a PaymentAPI problem rather than a seed problem.
4. Open **Buy Credits**. **PASS:** exactly three packages render — Starter, Professional,
   Enterprise — with **Professional (the middle one) visually featured**. **FAIL:** a different
   count, or no highlight. The view only highlights when the count is exactly 3
   (`Views/Payment/Index.cshtml`), so a fourth active package silently removes the highlight.

---

## 4 — Regression: genuinely out of credits still says so

**Proves:** the honest-failure work distinguishes "PaymentAPI is down" from "you have no money".
This is the check that stops item 6 from being a false pass — it would be easy to make everything
say "temporarily unavailable" and call the leak fixed.
**Rebuild:** no. **Destroys state:** no, but **use a fresh account** — the seeded candidate has
10,000 credits and cannot produce this state.

**Mechanism.** `InterviewAPI/Clients/IPaymentApiClient.cs` returns a `CreditBalanceResult` carrying
two independent flags:

- `Success=false, ServiceAvailable=true` → PaymentAPI answered and refused. Its message is safe to
  show the user.
- `Success=false, ServiceAvailable=false` → no answer at all. The message is log-only.

Steps: register a brand-new account through the UI, do **not** enrol biometrics (that grants a
one-time 100-credit bonus), and try to start a normal 15-minute interview, which costs 30 credits.

**PASS:** the UI shows, verbatim from PaymentAPI:

```
Insufficient credits. Required: 30, Available: 0.
```

**FAIL — shows "temporarily unavailable" or similar:** `ServiceAvailable` is not being honoured, and
every out-of-credits user is now being told the system is broken.
**FAIL — shows a raw exception, a stack frame, or a hostname:** a leak survived the C-01 sweep.

---

## 5 — The three message groups the C-01 sweep was told not to touch

**Proves:** the sweep that replaced 30 `ex.Message` leaks did not also flatten the messages that are
real user-facing copy. Each of these carries information the UI or the user depends on.
**Rebuild:** no. **Destroys state:** no. Needs one live interview session.

Start a normal interview as the seeded candidate and keep it open.

### 5a — Hint already used (machine-readable `ErrorCode`)

Use the hint on a question, then request a hint for the **same** question again.

**PASS:** HTTP **409**, body shaped `{ "error": "hint_already_used", "message": "A hint has already
been used for this question." }`. The Room UI branches on `error`, not on the prose.
**FAIL:** a generic `ApiResponse.Fail` with no `error` field, or a 400/503. The UI cannot tell this
apart from a real outage and will offer a pointless retry.

Check in the browser devtools Network tab, or:

```powershell
docker logs devprep-interview --since 5m | Select-String "hint_already_used"
```

### 5b — Biometric quality feedback arrives verbatim from ai-service

During the in-session face check, deliberately fail it — cover the camera, or point it at a blank
wall.

**PASS:** the message describes what is actually wrong and what to do — e.g. "No face detected",
"More than one face detected", or similar text originating in ai-service. It is relayed through
`BiometricQualityException`, which exists precisely so this text survives.
**FAIL:** "Face verification is temporarily unavailable. Please try again shortly." That string is
the *dependency-down* message; seeing it when ai-service is up and answering means the actionable
feedback was replaced with a generic one, and the candidate has no idea to uncover the lens.

### 5c — Validation still names the valid values

Send three bad configuration requests:

```powershell
# with a valid bearer token for the candidate
$h = @{ Authorization = "Bearer <TOKEN>"; "Content-Type" = "application/json" }
Invoke-WebRequest -Method POST "http://localhost:7000/api/interview/sessions/duration" -Headers $h -Body '{"durationMinutes":7}'   -UseBasicParsing
Invoke-WebRequest -Method POST "http://localhost:7000/api/interview/sessions/level"    -Headers $h -Body '{"level":"Wizard"}'      -UseBasicParsing
Invoke-WebRequest -Method POST "http://localhost:7000/api/interview/sessions/mode"     -Headers $h -Body '{"interactionMode":"Telepathy"}' -UseBasicParsing
```

**PASS:** the three responses say, respectively:

```
Invalid duration. Must be 15, 30, 45, or 60.
Invalid level.
Invalid interaction mode.
```

**FAIL:** "Something went wrong" or "temporarily unavailable". The duration message in particular
must keep listing the valid values — it is the only place the user learns them.

---

## 6 — Honest failure states

**Proves:** a dependency being down produces a state the user can interpret, with no internal
hostnames, ports, exception types, or framework text on screen.
**Rebuild:** `interviewapi`, `evaluationapi`, `webclient`. **Destroys state:** no — `unpause`
restores everything.

### 6a — Credit balance renders "—", not 0, when PaymentAPI is down

The distinction matters: `0` is a claim about the user's money and is a lie during an outage.

```powershell
docker pause devprep-payment
```

Now open **Profile → Credit History** as the candidate.

**PASS:** Balance, Total Used and Total Refunded all render **`—`**, and an error message says the
service is unavailable. The view models make these `int?` and the view renders `—` for null
(`Views/Profile/CreditHistory.cshtml:200,206,212`).
**FAIL — the three stat cards show `0 / 0 / 0`:** the failure path is returning a default view model
whose zeros are indistinguishable from real values. This is the exact defect recorded as C-02.
**FAIL — the page shows `502 (Bad Gateway)` or similar:** a framework message reached the browser.

```powershell
docker unpause devprep-payment
```

### 6b — ai-service down produces a friendly 503 with nothing internal on screen

```powershell
docker pause devprep-ai-node
```

Check all three screens that previously leaked. For each, the test is the same: read the entire
visible message and confirm **none** of the following appears anywhere — `ai-service`, `localhost`,
`http://`, a port number, `HttpRequestException`, `TaskCanceledException`, `HttpClient.Timeout`, or
a file path.

| Screen | How to trigger | Expected text |
|---|---|---|
| **Hint** | request a hint in a live session | `Hints are temporarily unavailable. Please try again shortly.` |
| **In-session verification warning** | let a periodic face check run | `Face verification is temporarily unavailable. Please try again shortly.` (voice check: `Voice verification is temporarily unavailable...`) |
| **CV/JD parsing** | upload a CV on Profile → CV & JD | `Document processing is temporarily unavailable. Please try again.` |

**PASS:** all three show their friendly message and HTTP 503, and the server log holds the real
detail:

```powershell
docker logs devprep-interview --since 5m | Select-String "Fetching a hint failed"
docker logs devprep-webclient  --since 5m | Select-String "Uploading a CV|Analyzing the CV"
```

**FAIL:** any internal token on screen, **or** an empty server log — if the detail is not logged,
the leak was closed by discarding the information rather than by relocating it, and the next
production incident is undebuggable.

```powershell
docker unpause devprep-ai-node
```

---

## 7 — Circuit breakers

**Proves:** the four breakers exist as singletons, count failures across calls, open, and recover.
**Rebuild:** `interviewapi`, `evaluationapi`. **Destroys state:** no.

**Mechanism.** `ResiliencePolicies` is registered as a **singleton** per service. That matters: a
breaker counts consecutive failures inside the policy object, so a per-request instance would reset
the count every call, look correctly wired, log nothing, and never open. Threshold is **5
consecutive failures**, break duration **30 seconds**.

Counted as failures: 5xx, 408, connection-level errors, and `TaskCanceledException` whose inner
exception is `TimeoutException` (i.e. a hung dependency). **Not** counted: ordinary 4xx, and caller
cancellations such as the user closing the tab.

The four breakers and their log identity:

| Breaker | Service | `{Dependency}` in the log |
|---|---|---|
| InterviewAPI → ai-service | `devprep-interview` | `ai-service` |
| InterviewAPI → PaymentAPI | `devprep-interview` | `PaymentAPI` |
| EvaluationAPI → ai-service | `devprep-evaluation` | `ai-service` |
| EvaluationAPI → InterviewAPI | `devprep-evaluation` | `InterviewAPI` |

### 7a — Drive one breaker Open → Half-Open → Closed

Take InterviewAPI → PaymentAPI as the example; the others follow the same shape.

```powershell
docker pause devprep-payment
```

Now make **at least five** calls that hit PaymentAPI — start an interview five times, or reload the
Buy Credits page five times. Then:

```powershell
docker logs devprep-interview --since 3m | Select-String "Circuit breaker"
```

**PASS — OPEN appears** (logged at Error):

```
Circuit breaker OPEN for PaymentAPI at <address> — blocking calls for 30s after 5 consecutive failures. Last failure: ...
```

Now restore the dependency and wait out the break:

```powershell
docker unpause devprep-payment
Start-Sleep -Seconds 32
```

Make one more call, then:

```powershell
docker logs devprep-interview --since 5m | Select-String "Circuit breaker"
```

**PASS — HALF-OPEN then CLOSED appear, in that order:**

```
Circuit breaker HALF-OPEN for PaymentAPI at <address> — break expired, letting one trial call through to test whether it recovered.
Circuit breaker CLOSED for PaymentAPI at <address> — the trial call succeeded, calls are flowing normally again.
```

**FAIL — no OPEN line after five or more failures:** the breaker is not accumulating. The usual
cause is `ResiliencePolicies` no longer being a singleton, or the policy attached with
`AddPolicyHandler`'s per-request overload. Both look correct in code review and never open.
**FAIL — HALF-OPEN appears but CLOSED never does:** the trial call is still failing; confirm the
dependency really is back with `docker ps`.

### 7b — Telling a breaker apart from a plain dependency failure

This is the distinction to be able to state out loud, because they look similar from the UI — both
produce a "temporarily unavailable" page.

| | Plain dependency failure | Breaker open |
|---|---|---|
| Log line | the caller's own error, e.g. `Fetching a hint failed...` | `Circuit breaker OPEN for ...` at **Error** level |
| Timing | each call waits the full timeout (15s / 30s / 60s) | fails **immediately**, no wait |
| Downstream | the dependency is contacted every time | the dependency is **not contacted at all** |

The immediacy is the giveaway. Once open, responses come back in milliseconds instead of after a
timeout — if the page still hangs for 30 seconds before erroring, the breaker is *not* open no
matter what you expect.

```powershell
docker logs devprep-interview --since 10m | Select-String "Circuit breaker OPEN"
docker logs devprep-evaluation --since 10m | Select-String "Circuit breaker OPEN"
```

### 7c — EvaluationAPI → InterviewAPI also retries once

This one breaker is wrapped in a retry, because both calls it makes are side-effect-free GETs.

```powershell
docker logs devprep-evaluation --since 10m | Select-String "Retrying InterviewAPI"
```

**PASS:** lines like `Retrying InterviewAPI at <address> in 2xx ms (attempt 1). Cause: ...`, at most
one per failed call.
**FAIL — more than one retry per call:** the retry count changed; re-check
`EvaluationAPI/Http/ResiliencePolicies.cs`.

---

## 8 — Demo session

**Proves:** the flag gates *pricing*, not merely the visibility of a dropdown option.
**Rebuild:** `interviewapi` and `webclient` on each flag change — the value is read at startup.
**Destroys state:** creates sessions and spends 6 credits each.

**Mechanism.** `DemoSession` defines 5 minutes / 3 questions / 6 credits. `IsDemoDuration` returns
true only when the duration is 5 **and** the flag is on, so with the flag off a 5-minute request
falls through to the normal whitelist and is rejected exactly as before the demo existed.

### 8a — Flag ON

```powershell
cd Q:\DevPrep\infra
$env:DEMO_SESSION_ENABLED = "true"
docker compose --env-file .env.docker -f docker-compose.yml up -d --build interviewapi webclient
```

Sign in as the candidate, note the credit balance, and start a 5-minute interview.

**PASS, all four:**
1. The **5-minute option appears** in the duration dropdown.
2. The session asks **exactly 3 questions** — the third completion ends the interview.
3. The balance drops by **exactly 6**.
4. `SessionQuestions` holds exactly 3 rows for that session:

```powershell
docker exec devprep-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT TOP 5 s.SessionId, s.DurationMinutes, COUNT(q.SessionQuestionId) AS questions FROM DevPrep_Interview.dbo.InterviewSessions s LEFT JOIN DevPrep_Interview.dbo.SessionQuestions q ON q.SessionId = s.SessionId GROUP BY s.SessionId, s.DurationMinutes, s.CreatedAt ORDER BY s.CreatedAt DESC"'
```

**FAIL — more than 3 rows:** the trim did not apply. ai-service plans 8 questions for a 5-minute
request and InterviewAPI keeps the first 3; confirm with

```powershell
docker logs devprep-interview --since 10m | Select-String "DEMO-SESSION: trimmed"
```

which should read `trimmed ai-service plan from 8 to 3 questions`.
**FAIL — charged 10 instead of 6:** the explicit `{ 5, 6 }` entry in `CreditConfig` was lost and the
2-credits-per-minute formula took over.

### 8b — Flag OFF (this is the half that matters)

```powershell
$env:DEMO_SESSION_ENABLED = "false"
docker compose --env-file .env.docker -f docker-compose.yml up -d --build interviewapi webclient
```

**Test 1 — the option disappears.** Reload the interview configuration page.
**PASS:** no 5-minute option in the dropdown.

**Test 2 — the API rejects it directly.** Hiding a dropdown is not protection; bypass the UI:

```powershell
$h = @{ Authorization = "Bearer <CANDIDATE_TOKEN>"; "Content-Type" = "application/json" }
Invoke-WebRequest -Method POST "http://localhost:7000/api/interview/sessions/duration" -Headers $h -Body '{"durationMinutes":5}' -UseBasicParsing
```

**PASS:** rejected with `Invalid duration. Must be 15, 30, 45, or 60.`
**FAIL — the call succeeds:** the flag only hides the UI. Any user who can send an HTTP request buys
a 6-credit interview that should cost 30, and the pricing model is unenforced. **Treat this as a
release blocker, not a cosmetic bug.**

---

## 9 — Regrade recovery — **UNVERIFIED**

> ### Status: built, never run end to end. Treat as unproven until this section passes.
>
> An earlier version of this feature was **dead code that passed review**: the recovery hook sat on
> a line reachable only under a race condition, so it never fired in normal operation and nothing in
> review caught it. That history is the reason this section exists in this much detail, and the
> reason it is the last item — it is both the least trusted and the only destructive one.
>
> **Do not report Phase 5 as verified if this section is skipped.**

**Proves:** when grading jobs are lost, opening the report page detects the gap and asks InterviewAPI
to re-queue exactly the missing answers — and logs which of the two situations it found.

**Mechanism.** When the report page is built, `EvaluationService` compares the session's substantive
answers against the evaluations that exist. If any are missing it logs one of two markers and calls
`RequestRegradeAsync` fire-and-forget. The two markers are deliberately separate log statements with
greppable leading tokens, not one template with a word interpolated, so a test grepping for the
wrong one fails loudly instead of matching stray prose:

- `REGRADE-TOTAL-LOSS` — zero evaluations exist; every grading job was lost.
- `REGRADE-PARTIAL` — some but not all answers were graded.

**Destroys state: yes. Each test below needs its own fresh session.**

### Test A — total loss

New session. Answer the first question, then **immediately**, before grading completes:

```powershell
docker pause devprep-ai-node
```

Wait ~30s so the in-flight grading job fails, then:

```powershell
docker unpause devprep-ai-node
```

Open that session's report page once. Then:

```powershell
docker logs devprep-evaluation --since 5m | Select-String "REGRADE-"
```

**PASS:** exactly one line, containing `REGRADE-TOTAL-LOSS`, and **no** `REGRADE-PARTIAL`.
**FAIL — no line at all:** the hook is not firing. This is the original dead-code failure mode
returning; the feature is not working regardless of what the code looks like.
**FAIL — `REGRADE-PARTIAL` instead:** an evaluation did land, so the precondition was not met — the
test is invalid rather than the feature. Retry with a fresh session and pause ai-service sooner.

### Test B — partial loss

New session. Answer question 1 and **let it grade fully** (confirm an evaluation exists before
continuing). Then pause ai-service, answer question 2, wait ~30s, unpause, and open the report.

```powershell
docker logs devprep-evaluation --since 5m | Select-String "REGRADE-"
```

**PASS:** exactly one line, containing `REGRADE-PARTIAL`, and **no** `REGRADE-TOTAL-LOSS`.
**FAIL — `REGRADE-TOTAL-LOSS`:** the first evaluation did not actually persist; the branch is
choosing on a count that is wrong.

### Test C — one line per page load, not three

Using either session above, reload the report page **once** and count:

```powershell
docker logs devprep-evaluation --since 2m | Select-String "REGRADE-" | Measure-Object | Select-Object -ExpandProperty Count
```

**PASS:** `1`.
**FAIL — `3`:** the report page builds from several sub-queries, and the de-duplication
(`_regradeAttemptedThisRequest`, a per-request set) is not holding. Each duplicate is a redundant
re-grade request for the same session, multiplying GPU load during an outage — exactly when the
system can least afford it.

### Test D — a healthy session must stay silent

New session, answer every question, let all grading complete normally, then open the report.

```powershell
docker logs devprep-evaluation --since 2m | Select-String "REGRADE-"
```

**PASS:** **no output at all.**
**FAIL — any `REGRADE-` line:** the gap detection has a false positive and every healthy report is
triggering pointless re-grades. This is the most expensive failure of the four, because it is
invisible from the UI — the report looks perfect while the system re-grades in the background on
every page view.

### After item 9

The sessions used above are permanently in a recovered-from-loss state. Do not reuse them for any
other test in this document, and do not treat their timings as representative.

---

## What Phase 5 does not cover

*Written to be read aloud. These are real limitations, stated plainly, because a limitation we name
is worth more than one a reviewer finds.*

**There is no message broker.** Every call between services is synchronous HTTP. Where the code says
"publish" or "event" — `AccountRegistered` is the clearest case — it is a direct HTTP POST with no
queue, no durability, and no retry. Nothing in Phase 5 changed that; the resilience work makes
synchronous failure *honest*, not asynchronous.

**There is no distributed tracing and no correlation ID.** A single user action can cross the
gateway, InterviewAPI, EvaluationAPI and ai-service, and there is no identifier tying those four log
streams together. Debugging a cross-service problem means matching timestamps by hand. The circuit
breakers log clearly, but they log *per service*, with no shared thread of a request.

**Grading recovery is pull-based, not a background sweeper.** Lost grading jobs are detected only
when a user opens the report page for that session. A session whose grading failed and whose report
is never opened stays un-graded indefinitely — nothing sweeps for it. That was a deliberate scope
decision, not an oversight: a sweeper needs a schedule, a work queue and its own failure handling,
and none of those exist yet.

**The regrade cooldown is in-memory and per-process.** It is a static dictionary inside InterviewAPI.
It does not survive a restart, so a redeploy clears every cooldown at once; and it does not
coordinate across replicas, so running two InterviewAPI instances would give each its own copy and
allow roughly double the intended re-grade rate. The system runs as a single instance today, which
is the only reason this is currently sufficient.

**Circuit-breaker state has the same limitation.** Each breaker lives inside one process. Two
replicas would fail independently — one could be open while the other hammers a dead dependency.

**Credit operations have no idempotency key.** A deduct or refund whose response is lost cannot be
safely retried, which is precisely why no automatic retry was added to that path. The compensating
refund on failure is best-effort and logged; there is no dead-letter queue and no reconciliation job.

**Health endpoints report liveness, not readiness.** Every `/health` returns a hardcoded 200 without
checking its database or dependencies. A service with an unreachable database still reports healthy.

**Gateway timeouts return the wrong status code.** A request that exceeds its route's budget reaches
the client as HTTP 400 rather than 504, because YARP writes the response before the timeout
middleware can. The cause is understood and the fix is documented in `ApiGateway/Program.cs`; it was
out of scope for the batch that introduced per-route timeouts.

**These checks are manual.** There is no automated test suite covering any of the above. This
document is the test suite, and it is only as reliable as the person running it.
