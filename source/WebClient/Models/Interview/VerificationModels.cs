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

// Mirrors InterviewAPI.DTOs.Biometrics.InSessionVerifyResponse.
public class InSessionVerifyResultModel
{
    public bool Passed { get; set; }
    public bool NoFaceDetected { get; set; }
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
}

// View model for the Interview/Verify page (biometric entry gate before the Room).
public class VerifyViewModel
{
    public Guid SessionId { get; set; }
    public int AttemptsRemaining { get; set; }
}
