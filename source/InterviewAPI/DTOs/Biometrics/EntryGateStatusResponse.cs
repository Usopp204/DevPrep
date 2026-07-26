namespace InterviewAPI.DTOs.Biometrics;

// Returned by GET api/interview/sessions/{sessionId}/verify/entry-gate — the decision the
// Verify screen (and the Room guard) checks before letting the candidate in. Face-only:
// entry requires an enrolled face plus one passed Entry/Face liveness check for THIS session.
// Attempts count failed tries per session; a new session starts fresh.
public class EntryGateStatusResponse
{
    public bool FaceEnrolled { get; set; }
    public bool Allowed { get; set; }
    public int AttemptsUsed { get; set; }
    public int AttemptsRemaining { get; set; }
    public string Reason { get; set; } = string.Empty;
}
