namespace InterviewAPI.DTOs.Biometrics;

// Returned by GET api/profiles/me/biometrics — per-modality enrollment state for the UI.
public class BiometricStatusResponse
{
    public bool FaceEnrolled { get; set; }
    public DateTime? FaceUpdatedAt { get; set; }
    public bool VoiceEnrolled { get; set; }
    public DateTime? VoiceUpdatedAt { get; set; }
}
