namespace InterviewAPI.Clients;

/// <summary>
/// Thrown when ai-service rejects a biometric sample for quality reasons
/// (HTTP 422: no face, blurry image, audio too short, ...). The message is
/// safe to surface to the end user.
/// </summary>
public class BiometricQualityException : Exception
{
    // Set only for the structured multi-face rejection from POST /internal/biometrics/verify
    // (see AiServiceClient.VerifyFaceAsync). Null for every other quality rejection (no face,
    // blurry, too small, ...), which still carry only a plain message like before.
    public int? FaceCount { get; }

    public BiometricQualityException(string message) : base(message)
    {
    }

    public BiometricQualityException(string message, int faceCount) : base(message)
    {
        FaceCount = faceCount;
    }
}
