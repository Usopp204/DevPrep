namespace InterviewAPI.DTOs.Biometrics;

// Returned by POST api/profiles/me/biometrics/{face|voice}. Never includes the embedding.
public class BiometricEnrollResponse
{
    public string Modality { get; set; } = string.Empty;
    public int Dim { get; set; }
    public double? Quality { get; set; }
}
