using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewAPI.Controllers;

[ApiController]
[Route("api/interview/sessions")]
[Authorize]
public class InterviewSessionController : ControllerBase
{
    private readonly ISessionService _service;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPaymentApiClient _paymentApiClient;

    public InterviewSessionController(ISessionService service, IAiServiceClient aiServiceClient, IPaymentApiClient paymentApiClient)
    {
        _service = service;
        _aiServiceClient = aiServiceClient;
        _paymentApiClient = paymentApiClient;
    }

    /// <summary>
    /// Danh mục role + công nghệ (proxy tới ai-service GET /catalog) để Dashboard đổ dropdown Role
    /// và chip Focus Areas theo dữ liệu thật. Không phụ thuộc phiên.
    /// </summary>
    [HttpGet("/api/interview/catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await _aiServiceClient.GetCatalogAsync(cancellationToken);
            return Ok(ApiResponse<InterviewAPI.DTOs.AiService.AiCatalogResponse>.Ok(catalog));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Đánh dấu user hiện tại đã xem/đóng Product Tour của Room (chỉ 1 lần trong đời user, không
    /// theo từng phiên). Gọi khi tour hoàn tất hoặc bị đóng/skip.
    /// </summary>
    [HttpPost("/api/interview/room-tour/seen")]
    public async Task<IActionResult> MarkRoomTourSeen(CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _service.MarkRoomTourSeenAsync(userId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { }, "Marked."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Lấy danh sách tất cả phiên phỏng vấn của user hiện tại (không bao gồm đã xoá mềm).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMySessions(CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GetMySessionsAsync(userId, cancellationToken);
            return Ok(ApiResponse<List<SessionListItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
    [HttpGet("history")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSessionHistory(
        [FromQuery] SessionHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _service.GetSessionHistoryAsync(userId, query, cancellationToken);
            return Ok(ApiResponse<SessionHistoryResult>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
    [HttpGet("{sessionId:guid}/chat-log")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetChatLog(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _service.GetSessionChatLogAsync(userId, sessionId, cancellationToken);
            return Ok(ApiResponse<SessionChatLogResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("feedback-eligibility")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetFeedbackEligibility(CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        var result = await _service.GetFeedbackEligibilityAsync(userId, cancellationToken);
        return Ok(ApiResponse<FeedbackEligibilityResponse>.Ok(result));
    }

    [HttpGet("{sessionId:guid}/feedback-eligibility")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSessionFeedbackEligibility(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _service.GetSessionFeedbackEligibilityAsync(userId, sessionId, cancellationToken);
            return Ok(ApiResponse<SessionFeedbackEligibilityResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:guid}/transcript")]
    public async Task<IActionResult> GetTranscript(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GetTranscriptAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<TranscriptResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Ownership check dùng nội bộ (EvaluationAPI gọi để chống IDOR): 200 nếu user trong JWT sở hữu
    /// phiên này, 404 nếu không (phiên không tồn tại/đã xoá/thuộc user khác).
    /// </summary>
    [HttpGet("{id:guid}/owns")]
    public async Task<IActionResult> Owns(Guid id, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var owns = await _service.OwnsSessionAsync(userId, id, cancellationToken);
        if (!owns)
            return NotFound(ApiResponse<object>.Fail("Session not found."));
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>Lấy thời gian còn lại và trạng thái hiện tại của phiên phỏng vấn.</summary>
    [HttpGet("{id:guid}/timer")]
    public async Task<IActionResult> GetTimer(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GetTimerAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<TimerResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Ghi nhận thời gian phản hồi của user cho một câu hỏi, cập nhật trung bình toàn phiên.</summary>
    [HttpPost("{id:guid}/track-time")]
    public async Task<IActionResult> TrackTime(
        Guid id, [FromBody] TrackTimeRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _service.TrackTimeAsync(userId, id, req, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { }, "Tracked."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Finalizes a naturally elapsed session as Completed or Expired and closes its room.</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CompleteSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.CompleteSessionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<EndResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Cancels a session when the Candidate explicitly ends the interview and closes its room.</summary>
    [HttpPost("{id:guid}/end")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> EndSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.EndSessionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<EndResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Tạm dừng phiên (tối đa 3 lần / tổng 300 giây mỗi phiên) — đóng băng timer + idle.</summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.PauseSessionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<PauseResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Bỏ tạm dừng phiên — cộng dồn thời gian đã pause, trả về timer đã tính lại.</summary>
    [HttpPost("{id:guid}/unpause")]
    public async Task<IActionResult> Unpause(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.UnpauseSessionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<UnpauseResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Tái tạo trạng thái phiên (lịch sử chat + câu hỏi hiện tại + timer) để client tiếp tục.</summary>
    [HttpGet("{id:guid}/resume")]
    public async Task<IActionResult> ResumeSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.ResumeSessionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<ResumeResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Xoá mềm phiên phỏng vấn của chính user (ẩn khỏi danh sách, không xoá DB).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _service.SoftDeleteAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Cấu hình thời lượng phỏng vấn (15/30/45/60 phút). Không có sessionId -> tạo phiên mới.</summary>
    [HttpPut("config/duration")]
    public async Task<IActionResult> ConfigDuration([FromBody] ConfigDurationRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.ConfigDurationAsync(userId, req, cancellationToken);
            return Ok(ApiResponse<ConfigResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Cấu hình cấp độ phỏng vấn (Intern/Fresher/Junior/Middle/Senior/Lead).</summary>
    [HttpPut("config/level")]
    public async Task<IActionResult> ConfigLevel([FromBody] ConfigLevelRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.ConfigLevelAsync(userId, req, cancellationToken);
            return Ok(ApiResponse<ConfigResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Cấu hình kiểu tương tác (Text hoặc Voice).</summary>
    [HttpPut("config/interaction")]
    public async Task<IActionResult> ConfigInteraction([FromBody] ConfigInteractionRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.ConfigInteractionAsync(userId, req, cancellationToken);
            return Ok(ApiResponse<ConfigResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Nộp câu trả lời của user cho câu hỏi hiện tại.
    /// Trả về câu hỏi tiếp theo (Ended=false) hoặc kết thúc phiên (Ended=true).
    /// </summary>
    [HttpPost("{id:guid}/message")]
    public async Task<IActionResult> SendMessage(
        Guid id, [FromBody] SendMessageRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.SendMessageAsync(userId, id, req, cancellationToken);
            return Ok(ApiResponse<MessageResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Lấy câu hỏi tiếp theo chưa được trả lời trong phiên.
    /// Trả về Ended=true khi đã hỏi hết tất cả câu hỏi trong kế hoạch.
    /// </summary>
    [HttpPost("{id:guid}/questions/generate")]
    public async Task<IActionResult> GenerateQuestion(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GenerateQuestionAsync(userId, id, cancellationToken);
            return Ok(ApiResponse<GenerateQuestionResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Chuyển văn bản thành giọng nói (proxy tới ai-service POST /tts). Trả về audio/mpeg.
    /// Không phụ thuộc session — chỉ đọc to nội dung câu hỏi AI cho client khi bật "AI Voice".
    /// </summary>
    [HttpPost("tts")]
    public async Task<IActionResult> TextToSpeech([FromBody] TtsRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(ApiResponse<object>.Fail("Text is required."));

        try
        {
            var audio = await _aiServiceClient.SynthesizeSpeechAsync(req.Text, cancellationToken);
            return File(audio, "audio/mpeg");
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Chuyển audio (ghi âm câu trả lời) thành văn bản (proxy tới ai-service /internal/stt).
    /// Stateless — chỉ transcribe; client tự đổ text vào ô nhập rồi gửi qua message như gõ tay.
    /// </summary>
    [HttpPost("stt")]
    public async Task<IActionResult> SpeechToText(IFormFile audio, CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Audio is required."));

        try
        {
            using var stream = new MemoryStream();
            await audio.CopyToAsync(stream, cancellationToken);
            var contentType = string.IsNullOrEmpty(audio.ContentType) ? "audio/webm" : audio.ContentType;
            var fileName = string.IsNullOrEmpty(audio.FileName) ? "answer.webm" : audio.FileName;

            var text = await _aiServiceClient.TranscribeAsync(
                stream.ToArray(), fileName, contentType, cancellationToken);
            return Ok(ApiResponse<SttResponse>.Ok(new SttResponse { Text = text }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Gợi ý cho câu hỏi hiện tại (proxy tới ai-service /internal/hint). Stateless — chỉ cần text
    /// câu hỏi; không lộ đáp án đầy đủ, trả về cùng ngôn ngữ với câu hỏi.
    /// </summary>
    [HttpPost("hint")]
    public async Task<IActionResult> Hint([FromBody] HintRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(ApiResponse<object>.Fail("Question is required."));

        try
        {
            var hint = await _aiServiceClient.GetHintAsync(req.Question, cancellationToken);
            return Ok(ApiResponse<HintResponse>.Ok(new HintResponse { Hint = hint }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Khởi động phiên phỏng vấn: gọi ai-service lấy danh sách câu hỏi, lưu DB, mở phòng phỏng vấn.
    /// Phiên phải ở trạng thái "Configured" trước khi gọi endpoint này.
    /// </summary>
    [HttpPost("init")]
    public async Task<IActionResult> Init([FromBody] InitRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.InitAsync(userId, req, cancellationToken);
            return Ok(ApiResponse<InitResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
    private bool TryGetAuthenticatedUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }

    [HttpGet("credit-balance")]
    public async Task<IActionResult> GetCreditBalance(CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var balance = await _paymentApiClient.GetBalanceAsync(userId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { Balance = balance }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail($"Failed to check credit balance: {ex.Message}"));
        }
    }
}
