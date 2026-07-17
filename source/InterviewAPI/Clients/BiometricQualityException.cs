namespace InterviewAPI.Clients;

/// <summary>
/// Thrown when ai-service rejects a biometric sample for quality reasons
/// (HTTP 422: no face, blurry image, audio too short, ...). The message is
/// safe to surface to the end user.
/// </summary>
public class BiometricQualityException : Exception
{
    public BiometricQualityException(string message) : base(message)
    {
    }
}
