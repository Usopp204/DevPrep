namespace InterviewAPI.DTOs.Biometrics;

// Returned by GET api/interview/sessions/{sessionId}/verify/in-session-check-due —
// randomized-cadence signal for whether a fresh in-session re-check should be prompted
// now (Milestone 4 groundwork; not wired into any UI yet).
public class InSessionCheckDueResponse
{
    public bool Due { get; set; }
    public double ElapsedMinutes { get; set; }
    public double TargetMinutes { get; set; }
}
