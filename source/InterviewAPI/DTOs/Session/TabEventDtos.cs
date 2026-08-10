namespace InterviewAPI.DTOs.Session;

// Client-reported focus-loss / paste signal from the Interview Room. Tab/WindowBlur are always
// record-only; Paste can end the session once the strike cap is reached (Phase 4) — see
// TabEventResponse below. IncidentId is the client-generated idempotency key — the Room must send
// the SAME value if it retries the report, so a flaky network cannot turn one tab switch into
// several audit rows (enforced by the UX_SVE_IncidentId filtered unique index behind
// TryAddIncidentAsync).
public class RecordTabEventRequest
{
    public Guid IncidentId { get; set; }

    // "TabHidden" | "WindowBlur" | "Paste" — see BiometricVerificationService.TabEventTypes.
    public string EventType { get; set; } = string.Empty;

    // How long the candidate was away, in milliseconds. Anything shorter than
    // Biometrics:TabSwitchMinAwayMs is treated as incidental and not recorded.
    public long DurationMs { get; set; }
}

// TabHidden/WindowBlur are written with Modality="Tab" — NOT in CheatingStrikeHelper's DIRECT
// strike whitelist, but they still accumulate the same way NoFace does: every
// Biometrics:TabStrikeThreshold of them converts into one effective strike (Phase 5). Paste is
// written with Modality="Paste", which IS in the direct whitelist (Phase 4): every single one is
// its own full strike. Either way a session can end once the shared strike cap is reached.
//
// TabCount/TabStrikeThreshold/TabJustBecameStrike (naming mirrors NoFaceCount/
// NoFaceStrikeThreshold/NoFaceJustBecameStrike on InSessionVerifyResponse) are populated for every
// freshly-recorded Tab/WindowBlur event, strike or not — they report progress toward the next
// conversion. The remaining strike fields (StrikeCount, MaxStrikes, ShouldEndSession, Status,
// TerminationReason, CanGeneratePerformanceReport, CanSubmitInterviewFeedback — naming mirrors
// InSessionVerifyResponse) are populated ONLY when the termination predicate was actually
// evaluated this call: a freshly-recorded Paste event, or a Tab/WindowBlur event where
// TabJustBecameStrike is true. Every other outcome (BelowThreshold, Duplicated, Disabled, a
// below-threshold-progress Tab/WindowBlur event) leaves them null, so the client can tell "no
// strike info was evaluated" apart from "zero strikes accumulated" instead of guessing from
// StrikeCount==0.
public class TabEventResponse
{
    // True only when this call actually wrote a new audit row.
    public bool Recorded { get; set; }

    // True when an event with this IncidentId already existed — the report is still considered
    // successful (200), it just did not write a second row.
    public bool Duplicated { get; set; }

    // Echoes back what the server decided to do, so the caller does not have to infer it from the
    // flag combination. One of BiometricVerificationService.TabOutcome*.
    public string Outcome { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public string Message { get; set; } = string.Empty;

    // Non-null exactly for a freshly-recorded Tab/WindowBlur event — see the class comment.
    // TabCount is progress toward the NEXT conversion (0..TabStrikeThreshold-1, or exactly 0 on
    // the event that just converted), the same shape as InSessionVerifyResponse.NoFaceCount.
    public int? TabCount { get; set; }
    public int? TabStrikeThreshold { get; set; }
    public bool? TabJustBecameStrike { get; set; }

    // Non-null exactly when Outcome==Recorded and (EventType=="Paste" OR TabJustBecameStrike) —
    // see the class comment.
    public int? StrikeCount { get; set; }
    public int? MaxStrikes { get; set; }
    public bool? ShouldEndSession { get; set; }
    public string? Status { get; set; }
    public string? TerminationReason { get; set; }
    public bool? CanGeneratePerformanceReport { get; set; }
    public bool? CanSubmitInterviewFeedback { get; set; }
}
