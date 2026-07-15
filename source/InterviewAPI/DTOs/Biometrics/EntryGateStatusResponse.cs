namespace InterviewAPI.DTOs.Biometrics;

// Returned by GET api/interview/sessions/{sessionId}/verify/entry-gate — a read-only
// decision the Room UI would check before letting the candidate in (Milestone 4
// groundwork; not wired into any UI yet).
public class EntryGateStatusResponse
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool? FacePassed { get; set; }
    public bool? VoicePassed { get; set; }
    public int? RetriesRemaining { get; set; }
}
