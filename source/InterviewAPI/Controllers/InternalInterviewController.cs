using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InterviewAPI.Models; 

namespace InterviewAPI.Controllers;

[ApiController]
[Route("api/internal/interviews")] // đường dẫn gọi từ UserAPI
public class InternalInterviewController : ControllerBase
{
    private readonly InterviewDbContext _db;

    
    public InternalInterviewController(InterviewDbContext db)
    {
        _db = db;
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessionsByUserId([FromQuery] Guid userId, CancellationToken ct)
    {
        try
        {
            // Truy vấn trực tiếp từ database lấy danh sách Session theo UserId
            var sessions = await _db.InterviewSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    SessionId = s.SessionId,
                    Title = s.Position ?? "Chưa xác định",
                    DurationMinutes = s.DurationMinutes,
                    Level = s.Level,
                    InteractionMode = s.InteractionMode,
                    QuestionType = s.QuestionType,
                    Status = s.Status,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    TotalQuestions = s.TotalQuestions,
                    // Đếm số tin nhắn dạng "Answer" mà "User" đã trả lời trong session này
                    AnsweredQuestions = _db.ChatMessages.Count(m =>
                        m.SessionQuestion.SessionId == s.SessionId
                        && m.SenderType == "User"
                        && m.MessageType == "Answer"),
                    CreditCost = s.CreditCost 
                })
                .ToListAsync(ct);

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}