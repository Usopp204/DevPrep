using InterviewAPI.DTOs.AiService;

namespace InterviewAPI.Clients;

public interface IAiServiceClient
{
    Task<AiQuestionPlanResponse> PlanQuestionsAsync(AiQuestionPlanRequest request, CancellationToken ct = default);
    Task<AiEvaluateAnswerResponse> EvaluateAnswerAsync(AiEvaluateAnswerRequest request, CancellationToken ct = default);
    Task<AiBuildReportResponse> BuildReportAsync(AiBuildReportRequest request, CancellationToken ct = default);

    // Synthesizes the given text to speech via ai-service POST /tts (audio/mpeg).
    // Returns the raw mp3 bytes (clips are short, so buffering is simplest and safe).
    Task<byte[]> SynthesizeSpeechAsync(string text, CancellationToken ct = default);

    // Transcribes an uploaded audio clip via ai-service POST /internal/stt. Returns the text.
    Task<string> TranscribeAsync(byte[] audio, string fileName, string contentType, CancellationToken ct = default);

    // Gets one light directional hint for a question via ai-service POST /internal/hint.
    Task<string> GetHintAsync(string question, CancellationToken ct = default);

    // Gets the roles + technologies catalog via ai-service GET /catalog (drives the dashboard).
    Task<AiCatalogResponse> GetCatalogAsync(CancellationToken ct = default);

    // Extracts a face embedding via ai-service POST /internal/biometrics/face/embed.
    // Throws BiometricQualityException when the sample is rejected (HTTP 422).
    Task<BiometricEmbeddingResult> EmbedFaceAsync(IFormFile file, CancellationToken ct = default);

    // Extracts a voice embedding via ai-service POST /internal/biometrics/voice/embed.
    // Throws BiometricQualityException when the sample is rejected (HTTP 422).
    Task<BiometricEmbeddingResult> EmbedVoiceAsync(IFormFile file, CancellationToken ct = default);

    // Face liveness + identity check via POST /internal/biometrics/face/verify-live.
    // challengeJson is the stored server-side expected value, e.g. {"type":"turn_head","direction":"right"}.
    Task<AiFaceVerifyLiveResponse> VerifyFaceLiveAsync(
        IReadOnlyList<IFormFile> frames, float[] storedEmbedding, string challengeJson,
        CancellationToken ct = default);

    // Single-frame identity match via POST /internal/biometrics/verify — no challenge, no
    // liveness/burst. Used by the in-session cheating check (VerifyFaceLiveAsync/face-verify-live
    // stays the one entry verification uses). Throws BiometricQualityException with FaceCount
    // set when the rejection reason is specifically "multiple faces detected".
    Task<AiFaceVerifyResponse> VerifyFaceAsync(
        IFormFile frame, float[] storedEmbedding, CancellationToken ct = default);

    // Voice speaker + challenge-phrase check via POST /internal/biometrics/voice/verify-challenge.
    Task<AiVoiceVerifyChallengeResponse> VerifyVoiceChallengeAsync(
        IFormFile file, float[] storedEmbedding, string expectedPhrase,
        CancellationToken ct = default);
}
