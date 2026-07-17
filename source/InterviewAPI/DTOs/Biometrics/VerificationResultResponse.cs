namespace InterviewAPI.DTOs.Biometrics;

// Returned by POST api/interview/sessions/{sessionId}/verify/{face|voice}.
// Raw verification outcome; decision policy (retries, flagging, gating) is Milestone 4.
public class VerificationResultResponse
{
    public string Modality { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double? Score { get; set; }      // face: mean identity score; voice: speaker score
    public bool? Live { get; set; }         // face only
    public bool? ContentOk { get; set; }    // voice only
    public string? Transcript { get; set; } // voice only (helps the user see what STT heard)
    public string? Reason { get; set; }
}
