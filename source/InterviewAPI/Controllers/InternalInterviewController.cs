using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InterviewAPI.Models;
using InterviewAPI.Services; 

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

    // Trang admin user detail hỏi trạng thái enroll khuôn mặt/giọng nói.
    //
    // Nguồn duy nhất đáng tin là bảng ProfileBiometrics ở đây. Hai cột
    // Accounts.IsFaceVerified/IsVoiceVerified bên AuthAPI KHÔNG có code nào ghi vào — chúng
    // luôn bằng 0 — nên admin phải đọc thẳng chỗ này thay vì nhân bản cờ sang AuthAPI.
    //
    // userId nhận vào là AccountId, khớp với cột ProfileBiometrics.UserId mà luồng enroll
    // đang ghi (xem endpoint sessions ở trên cũng nhận AccountId).
    [HttpGet("biometrics")]
    public async Task<IActionResult> GetBiometricsByUserId([FromQuery] Guid userId, CancellationToken ct)
    {
        try
        {
            var modalities = await _db.ProfileBiometrics
                .Where(b => b.UserId == userId)
                .Select(b => b.Modality)
                .ToListAsync(ct);

            return Ok(new
            {
                FaceEnrolled = modalities.Contains(ProfileBiometricsService.ModalityFace),
                VoiceEnrolled = modalities.Contains(ProfileBiometricsService.ModalityVoice)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}