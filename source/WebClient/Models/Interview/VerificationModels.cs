namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Biometrics.VerificationChallengeResponse.
public class VerificationChallengeModel
{
    public string Modality { get; set; } = string.Empty;
    public string? Direction { get; set; }
    public string? Phrase { get; set; }
    public int ExpiresInSeconds { get; set; }
}

// Mirrors InterviewAPI.DTOs.Biometrics.VerificationResultResponse.
public class VerificationResultModel
{
    public string Modality { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double? Score { get; set; }
    public bool? Live { get; set; }
    public bool? ContentOk { get; set; }
    public string? Transcript { get; set; }
    public string? Reason { get; set; }
}

// Mirrors InterviewAPI.DTOs.Biometrics.EntryGateStatusResponse (face-only entry gate).
public class EntryGateStatusModel
{
    public bool FaceEnrolled { get; set; }
    public bool Allowed { get; set; }
    public int AttemptsUsed { get; set; }
    public int AttemptsRemaining { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Mirrors InterviewAPI.DTOs.Biometrics.InSessionVerifyResponse exactly so the same-origin
// proxy does not discard fields that interview-room.js needs for warning classification.
public class InSessionVerifyResultModel
{
    public bool Passed { get; set; }
    public bool NoFaceDetected { get; set; }
    public string WarningType { get; set; } = "None";
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public int NoFaceCount { get; set; }
    public int NoFaceStrikeThreshold { get; set; }
    public bool NoFaceJustBecameStrike { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
}

// Body the Room posts to the same-origin Verification/RecordTabEvent proxy. Mirrors
// InterviewAPI.DTOs.Session.RecordTabEventRequest; IncidentId is generated in the browser
// (crypto.randomUUID) so a retry — including the sendBeacon on pagehide — cannot double-record.
public class TabEventRequestModel
{
    public Guid IncidentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

// Mirrors InterviewAPI.DTOs.Session.TabEventResponse. Tab/WindowBlur are outside the server's
// DIRECT strike whitelist but still accumulate the same way NoFace does — every
// Biometrics:TabStrikeThreshold of them converts into one effective strike (Phase 5). Paste is
// always its own full strike (Phase 4). Either way the session's shared strike pool can end it.
//
// TabCount/TabStrikeThreshold/TabJustBecameStrike (mirrors NoFaceCount/NoFaceStrikeThreshold/
// NoFaceJustBecameStrike on InSessionVerifyResultModel) are set for every freshly-recorded
// Tab/WindowBlur event. The remaining strike fields are set ONLY when the termination predicate
// was actually evaluated this call — a freshly-recorded Paste event, or a Tab/WindowBlur event
// where TabJustBecameStrike is true. Every other outcome (BelowThreshold, Duplicated, Disabled, a
// below-threshold Tab/WindowBlur progress event) leaves them null, which is how the Room tells
// "no strike info" apart from "zero strikes".
public class TabEventResultModel
{
    public bool Recorded { get; set; }
    public bool Duplicated { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? TabCount { get; set; }
    public int? TabStrikeThreshold { get; set; }
    public bool? TabJustBecameStrike { get; set; }
    public int? StrikeCount { get; set; }
    public int? MaxStrikes { get; set; }
    public bool? ShouldEndSession { get; set; }
    public string? Status { get; set; }
    public string? TerminationReason { get; set; }
    public bool? CanGeneratePerformanceReport { get; set; }
    public bool? CanSubmitInterviewFeedback { get; set; }
}

public class InSessionCheckDueModel
{
    public bool Due { get; set; }
    public double ElapsedMinutes { get; set; }
    public double TargetMinutes { get; set; }
    public Guid? CheckId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool ShouldEndSession { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
}

// View model for the Interview/Verify page (biometric entry gate before the Room).
public class VerifyViewModel
{
    public Guid SessionId { get; set; }
    public int AttemptsRemaining { get; set; }
}
