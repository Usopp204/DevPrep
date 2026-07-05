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
}
