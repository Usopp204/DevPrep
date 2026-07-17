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
