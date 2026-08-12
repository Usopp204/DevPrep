# Code Follow-ups

Deferred items in the **application code**, found while doing other work and consciously left out of
the batch that found them. Recorded so they are not lost between batches.

(For deferred items about the development harness — permissions, hooks, tooling — see
`tooling-followups.md`. Different subject, different file, on purpose.)

---

## C-01 — 31 controller actions return raw `ex.Message` to the client

**Status:** deferred by decision, Batch 5.1. Not scheduled. **Good candidate for a standalone PR:**
mechanical, low-risk, no behaviour change beyond the message text, easy to review in one pass.

**Found:** while fixing the two actions Batch 5.1 owned. The batch fixed 2 of 33; the other 31 were
left alone deliberately rather than expanding a resilience batch into a codebase-wide edit.

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

### Where

| File | Occurrences | Notes |
|---|---|---|
| `source/InterviewAPI/Controllers/InterviewSessionController.cs` | **21** remaining | 23 found; Batch 5.1 fixed `Init` and `GetCreditBalance`. One returns `NotFound`, the rest `BadRequest` |
| `source/InterviewAPI/Controllers/SessionVerificationController.cs` | 7 | all `BadRequest(ApiResponse<object>.Fail(ex.Message))` |
| `source/InterviewAPI/Controllers/ProfileBiometricsController.cs` | 2 | same shape |
| `source/InterviewAPI/Controllers/InternalInterviewController.cs` | 1 | `BadRequest(new { message = ex.Message })` — different envelope, same leak |

All four files are InterviewAPI, which is owned code — **no teammate coordination needed.**
EvaluationAPI was checked and does **not** have this pattern: its controller already maps typed
exceptions to 503 and returns fixed message strings (`EvaluationController.cs:38-42`).

### The fix, when scheduled

Per action: log the exception server-side with `ILogger` (detail preserved for debugging), return a
fixed, non-revealing message to the client. Where a typed exception already distinguishes a
dependency failure, route it to 503 first — the shape Batch 5.1 used for `Init`:

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
