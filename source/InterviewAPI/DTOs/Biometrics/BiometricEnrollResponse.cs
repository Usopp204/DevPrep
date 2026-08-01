namespace InterviewAPI.DTOs.Biometrics;

// Returned by POST api/profiles/me/biometrics/{face|voice}. Never includes the embedding.
public class BiometricEnrollResponse
{
    public string Modality { get; set; } = string.Empty;
    public int Dim { get; set; }
    public double? Quality { get; set; }

    // Outcome of the one-time enrollment bonus for THIS call. One of (see
    // ProfileBiometricsService.BonusStatus* constants):
    //   Awarded                — this call is what granted the 100-credit bonus.
    //   AlreadyAwarded         — this account already received the bonus (normal re-enrollment).
    //   IdentityAlreadyClaimed — an earlier account with the same face already claimed it.
    //   NotAwarded             — the PaymentAPI award call did not succeed.
    //   NotYetEligible         — the other modality isn't enrolled yet.
    public string BonusStatus { get; set; } = string.Empty;
}
