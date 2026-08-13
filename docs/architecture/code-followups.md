# Code Follow-ups

Deferred items in the **application code**, found while doing other work and consciously left out of
the batch that found them. Recorded so they are not lost between batches.

(For deferred items about the development harness — permissions, hooks, tooling — see
`tooling-followups.md`. Different subject, different file, on purpose.)

---

## C-01 — controller actions returning raw `ex.Message` to the client

**Status: MOSTLY CLOSED.** 32 of the original 33 candidate-facing cases are fixed (2 in Batch 5.1,
30 in the C-01 candidate-facing batch). **3 cases remain**, all deliberately out of the scopes taken
so far — see "What is actually left" below.

**History:** found while fixing the two actions Batch 5.1 owned, originally recorded as "31
remaining across 4 files". The candidate-facing batch then took all three candidate-facing files to
zero. This section was re-verified against the code on the date of Batch 5.5b — the counts below are
from a fresh grep, not from the original survey.

### The pattern

```csharp
catch (Exception ex)
{
    return BadRequest(ApiResponse<object>.Fail(ex.Message));   // <- ex.Message goes to the browser
}
```

Two problems, in order of severity:

1. **Information disclosure.** `ex.Message` on an unhandled exception can carry connection strings,
   file paths, internal host names, SQL fragments, or a stack-adjacent detail from a nested
   dependency. None of it is meaningful to a user and all of it is useful to an attacker.
2. **Wrong status code.** A catch-all `BadRequest` answers 400 — "your request was malformed" — for
   failures that are entirely server-side (a dependency being down, a DB timeout). Clients cannot
   distinguish "fix your input and retry" from "our problem, retry later," and neither can a log
   dashboard filtering on 4xx vs 5xx.

### Done — candidate-facing, all at zero

| File | Was | Now |
|---|---|---|
| `source/InterviewAPI/Controllers/InterviewSessionController.cs` | 23 | **0** — 2 in Batch 5.1, 21 in the candidate-facing batch |
| `source/InterviewAPI/Controllers/SessionVerificationController.cs` | 7 | **0** |
| `source/InterviewAPI/Controllers/ProfileBiometricsController.cs` | 2 | **0** |

Every remaining `ex.Message` in those three files sits on a **typed** catch and is deliberate —
`SessionGuardException` (carries a machine-readable `ErrorCode` WebClient parses),
`BiometricQualityException` (relays ai-service's actionable "No face detected" text),
`KeyNotFoundException` ("Session not found."), and the duration/level/mode validation messages.
Each is marked with a `// KEEP ex.Message` comment. **Do not "finish the job" on those** — a blanket
replacement makes the UI worse, which is the trap this note exists to prevent.

Those actions also moved from `400` to `503`: a generic catch-all sitting *after* the typed catches
has already filtered out client errors, so what reaches it is a dependency failure or a bug —
never the caller's fault.

### What is actually left

| File | Occurrences | Why it was left |
|---|---|---|
| `source/InterviewAPI/Controllers/InternalInterviewController.cs:51-53` | 1 | `BadRequest(new { message = ex.Message })`. **Service-to-service only** (UserAPI calls it), never rendered to a candidate, so it was excluded from the candidate-facing scope. Still worth fixing — an internal caller logging the response would capture the same detail |
| `source/WebClient/Controllers/News/AdminNewsController.cs:186, 275` | 2 | `ViewBag.ErrorMessage = ex.Message` rendered on an admin page. Deferred with the rest of the admin-facing work |

**`AdminInterviewController.cs` was checked and has none** — it has no `catch (Exception ex)` blocks
at all, so "the admin controllers" is narrower than it sounds: only the WebClient news admin pages.

**EvaluationAPI is clean.** Its controller's five `ex.Message` uses are all on typed catches
(`InterviewApiSessionNotFoundException`, `SessionNotEligibleForPerformanceReportException`,
`AggregateReportNotReadyException`) — authored copy, not leaks.

**Out of scope entirely:** `QuestionAPI/Controllers/AdminQuestionController.cs` has the same pattern
in volume (`ex.InnerException?.Message ?? ex.Message`), but QuestionAPI is teammate-owned code and
is not covered by this note.

### The fix, for the 3 that remain

This is the shape the 32 completed ones used — follow it rather than inventing a new one. Log the
exception server-side with `ILogger` (detail preserved for debugging), return a fixed,
non-revealing message. Where a typed exception already distinguishes a dependency failure, route it
to 503 first:

```csharp
catch (SomeDependencyException ex)
{
    _logger.LogWarning(ex, "...");
    return StatusCode(StatusCodes.Status503ServiceUnavailable,
        ApiResponse<object>.Fail("... is temporarily unavailable. Please try again shortly."));
}
catch (Exception ex)
{
    _logger.LogError(ex, "...");
    return BadRequest(ApiResponse<object>.Fail("<generic message for this action>"));
}
```

**Watch for** actions whose `ex.Message` is currently load-bearing — where a service throws
`InvalidOperationException("Session already completed.")` and the UI displays that string as real
user-facing copy. Those need a typed exception or an explicit message, not a blanket generic string,
or the fix silently degrades a working error message into "Something went wrong." Check each action
against what WebClient renders before replacing its message.

---

## C-02 — `WebClient` service layer surfaces `ex.Message` into view models

**Status: CLOSED**, Batch 5.1 follow-up. Fixed after testing surfaced it on a real page.

`source/WebClient/Services/Payment/PaymentService.cs` set `ErrorMessage = ex.Message` in
`GetBuyCreditsAsync`, `GetTransactionHistoryAsync`, and `GetCreditHistoryAsync`, and the Razor views
rendered it — the Credit Wallet page showed
`Error: Response status code does not indicate success: 502 (Bad Gateway).` during a PaymentAPI
outage. All three now log the exception and show a fixed `UnavailableMessage`.

**The bigger half of this finding was not the message but the numbers beside it.** The catch branches
returned a default view model, so the stat cards rendered `0 / 0 / 0` for Balance, Total Used and
Total Refunded as though those were confirmed values. `CreditHistoryViewModel.{Balance, TotalUsed,
TotalRefunded}` and `TransactionHistoryViewModel.Balance` are now nullable, and both views render
`—` for null.

**Correction recorded on purpose:** Batch 5.1 added a code comment claiming the `?? new()` in these
two methods was safe because "if PaymentAPI were down, `EnsureSuccessStatusCode` would already have
thrown and this page would render its error state instead, so the balance is never displayed." The
first clause was right; the conclusion was wrong — the error state *also* renders the stat cards.
The comment has been replaced.

---

## C-03 — EvaluationAPI's regrade call shares a circuit breaker with the calls that matter

**Status:** accepted risk, Batch 5.5b. Not scheduled. Revisit if the trigger below is ever observed.

### The shape

`EvaluationAPI/Program.cs` attaches `ResiliencePolicies.InterviewApi` to the **typed client**, not to
individual methods:

```csharp
builder.Services.AddHttpClient<IInterviewApiClient, InterviewApiClient>(...)
    .AddPolicyHandler((sp, _) => sp.GetRequiredService<ResiliencePolicies>().InterviewApi);
```

So one breaker instance is shared by everything on that client:

| Method | Matters because |
|---|---|
| `OwnsSessionAsync` | IDOR guard on every evaluation endpoint |
| `GetSessionEvaluationContextAsync` | transcript needed to build any report |
| `RequestRegradeAsync` | **recovery only** — fire-and-forget, never surfaced to the user |

### The risk

A persistent **5xx** bug in InterviewAPI's `POST /api/interview/sessions/{id}/regrade-missing` would
count toward that shared breaker. Five consecutive failures open it for **30 seconds**, during which
ownership checks and transcript fetches also fail — a recovery feature taking report viewing down
with it.

### Why it was accepted rather than fixed

- **The cooldown throttles the input.** InterviewAPI applies a 60s per-session cooldown, and a
  suppressed burst returns **200**, not an error. Reaching five consecutive 5xx responses therefore
  needs five *different* sessions inside one breaker window — not one user refreshing.
- **4xx does not count.** `HandleTransientHttpError()` handles 5xx, 408 and connection-level errors
  only, so an ordinary rejected regrade (404 unknown session, 400 bad body) is inert.
- **When InterviewAPI is genuinely unwell, opening the breaker is correct** — ownership and
  transcript calls would be failing anyway, and fast failure beats 15s timeouts.
- **Failure is loud.** The breaker logs `Circuit breaker OPEN for InterviewAPI`, so this degradation
  announces itself rather than hiding.

### The isolation option, if it is ever needed

Register a **separate named `HttpClient`** for the regrade call with its own policy (or no breaker
at all, since the call is already fire-and-forget and its failure is harmless by construction):

```csharp
builder.Services.AddHttpClient("interview-regrade", client => { /* same BaseAddress + timeout */ });
// no AddPolicyHandler, or a dedicated breaker instance
```

`RequestRegradeAsync` would then move off the typed client onto `IHttpClientFactory`, which is why
this was not done inside Batch 5.5b: it requires an `EvaluationAPI/Program.cs` change outside that
batch's scope and reshapes the method that was just written.

**Trigger to revisit:** any occurrence of `Circuit breaker OPEN for InterviewAPI` in the logs whose
preceding failures are regrade calls rather than ownership/transcript calls.

---

## C-04 — `CvJdService` presents a failed fetch as "no document on file"

**Status:** tracked, not scheduled. Logging added in Batch 5.3; the behaviour is unchanged and
still wrong. Needs a contract change, which is why it was split out rather than folded into 5.3.

### The shape

`source/WebClient/Services/CvJd/CvJdService.cs` — `GetCvInfoAsync` and `GetJdInfoAsync`:

```csharp
catch (Exception ex)
{
    logger.LogWarning(ex, "Fetching CV info failed; the page will show it as 'no CV on file'.");
    return null;   // <- indistinguishable from "the user has no CV"
}
```

`CvJdController.GetCvInfo` maps `null` to `NotFound()` (`CvJdController.cs:17`), and
`Views/Profile/CvJd.cshtml:854-857` treats a non-OK response as "nothing uploaded yet". So a
gateway outage, an expired token, or a timeout all render as **an empty CV panel** — the user is
told they have no CV when in fact we could not find out.

**Why this is worse than a normal swallowed exception:** the other seven methods in this class
return an `{ error }` payload the page renders as a visible message. These two return a value that
the UI reads as *successfully determined absence*. The failure is not merely hidden, it is
misreported as a fact about the user's data.

### Why Batch 5.3 did not fix it

The other seven methods return `Task<string?>` where `null` has no meaning, so a JSON error payload
slots in with no contract change. Here `null` is already load-bearing — it *is* the "no document"
signal. Distinguishing "failed" from "none" means the return type has to carry both, e.g.

```csharp
public record CvJdFetch(string? Json, bool Failed);
```

which changes `ICvJdService`, both controller actions, and the two fetch sites in
`Views/Profile/CvJd.cshtml` plus `wwwroot/js/pages/dashboard.js:293` (which does
`if (!jd || jd.error) return;` — it would need a third branch for "unknown").

### The fix, when scheduled

1. Give the two methods a return type that separates `Failed` from `Json is null`.
2. Controller: keep `404` for a genuine absence, return `503` for `Failed`.
3. Page: render "Could not load your CV. Please refresh." for `503`, leaving the existing
   empty-state copy for `404`.

**Do not** simply make these return the `{ error }` payload like the other seven — the callers at
`CvJd.cshtml:857,871` check `!cv.error` to decide whether to *populate the form*, so an error
payload would be parsed as document content.
