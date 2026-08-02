namespace InterviewAPI.Clients;

public class BiometricIdentityMismatchException : Exception
{
    public BiometricIdentityMismatchException(string modality, double score, double threshold)
        : base($"The new {modality.ToLowerInvariant()} sample does not match the currently enrolled identity.")
    {
        Modality = modality;
        Score = score;
        Threshold = threshold;
    }

    public string Modality { get; }
    public double Score { get; }
    public double Threshold { get; }
}
