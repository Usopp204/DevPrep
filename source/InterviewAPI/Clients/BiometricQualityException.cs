namespace InterviewAPI.Clients;

/// <summary>
/// Thrown when ai-service rejects a biometric sample for quality reasons
/// (HTTP 422: no face, blurry image, audio too short, ...). The message is
/// safe to surface to the end user.
/// </summary>
public class BiometricQualityException : Exception
{
    // Set for structured face-quality rejections from POST /internal/biometrics/verify.
    public string? QualityCode { get; }

    // Set only when QualityCode is "multiple_faces".
    public int? FaceCount { get; }

    public BiometricQualityException(string message) : base(message)
    {
    }

    public BiometricQualityException(string message, int faceCount) : base(message)
    {
        FaceCount = faceCount;
    }

    public BiometricQualityException(string message, string? qualityCode, int? faceCount = null)
        : base(message)
    {
        QualityCode = qualityCode;
        FaceCount = faceCount;
    }
}
