namespace InterviewAPI.DTOs.Biometrics;

// Returned by POST api/profiles/me/biometrics/{face|voice}. Never includes the embedding.
public class BiometricEnrollResponse
{
    public string Modality { get; set; } = string.Empty;
    public int Dim { get; set; }
    public double? Quality { get; set; }

    // True when THIS call is what completed enrollment for both Face and Voice, and the
    // one-time credit bonus was awarded. False on every other call, including re-enrollment
    // after the bonus was already granted once.
    public bool BonusAwarded { get; set; }
}
