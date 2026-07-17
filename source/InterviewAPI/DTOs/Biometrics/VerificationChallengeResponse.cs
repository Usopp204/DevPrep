namespace InterviewAPI.DTOs.Biometrics;

// Returned by GET api/interview/sessions/{sessionId}/verify/challenge.
// Carries only the instruction the user must perform — the expected value the
// server will verify against stays in SessionVerificationChallenges.
public class VerificationChallengeResponse
{
    public string Modality { get; set; } = string.Empty; // "Face" | "Voice"
    public string? Direction { get; set; }               // face: "left" | "right"
    public string? Phrase { get; set; }                  // voice: the sentence to read aloud
    public int ExpiresInSeconds { get; set; }
}
