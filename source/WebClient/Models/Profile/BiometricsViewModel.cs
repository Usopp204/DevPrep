using WebClient.Models.Interview;

namespace WebClient.Models.Profile;

// Mirrors InterviewAPI GET api/profiles/me/biometrics response data.
public class BiometricStatusModel
{
    public bool FaceEnrolled { get; set; }
    public DateTime? FaceUpdatedAt { get; set; }
    public bool VoiceEnrolled { get; set; }
    public DateTime? VoiceUpdatedAt { get; set; }
}

// Mirrors InterviewAPI.DTOs.Biometrics.BiometricEnrollResponse.
public class BiometricEnrollModel
{
    public string Modality { get; set; } = string.Empty;
    public int Dim { get; set; }
    public double? Quality { get; set; }

    // One of: Awarded, AlreadyAwarded, IdentityAlreadyClaimed, NotAwarded, NotYetEligible.
    public string BonusStatus { get; set; } = string.Empty;
}

// View model for Profile/Identity (biometric enrollment page).
public class BiometricsViewModel
{
    public BiometricStatusModel Status { get; set; } = new();
    public string? ErrorMessage { get; set; }

    // Recent sessions for the temporary "Test verification" section (Milestone 2).
    // Removed in Milestone 4 when the modal moves to the Room entry gate.
    public List<SessionHistoryItemDto> RecentSessions { get; set; } = new();
}
