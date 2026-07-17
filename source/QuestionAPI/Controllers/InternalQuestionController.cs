using Microsoft.AspNetCore.Mvc;
using QuestionAPI.Services;

namespace QuestionAPI.Controllers;

/// <summary>
/// Internal-only endpoints, protected by the shared X-Internal-Api-Key header
/// (same convention as UserAPI/NotificationAPI /internal/* endpoints).
/// Not routed through the Gateway — internal callers hit the service port directly.
/// </summary>
[ApiController]
[Route("internal/questions")]
[ApiExplorerSettings(IgnoreApi = true)]
public class InternalQuestionController : ControllerBase
{
    private readonly InternalQuestionService _service;
    private readonly string _internalApiKey;

    public InternalQuestionController(InternalQuestionService service, IConfiguration configuration)
    {
        _service = service;
        _internalApiKey = configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty)!;
    }

    /// <summary>
    /// GET /internal/questions/export — the full non-deleted question bank as a flat JSON array.
    /// Consumed by ai-service to seed its RAG index (replaces the old CSV source).
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!IsInternalCallAuthorized())
            return Unauthorized();

        var questions = await _service.ExportAllAsync(cancellationToken);
        return Ok(questions);
    }

    // So khớp header X-Internal-Api-Key với khoá cấu hình. Chưa cấu hình khoá -> cho qua (tiện chạy dev).
    private bool IsInternalCallAuthorized()
    {
        if (string.IsNullOrEmpty(_internalApiKey)) return true;
        return Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided)
            && string.Equals(provided.ToString(), _internalApiKey, StringComparison.Ordinal);
    }
}
