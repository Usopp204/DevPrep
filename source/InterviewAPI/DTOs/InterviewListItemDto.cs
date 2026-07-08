namespace InterviewAPI.DTOs;

/// <summary>Một dòng trong danh sách phiên phỏng vấn (admin). Kèm danh tính người dùng (resolve từ Auth/User).</summary>
public class InterviewListItemDto
{
    public Guid SessionId { get; set; }

    // Người sở hữu phiên — UserId luôn có; Email/FullName resolve từ AuthAPI/UserAPI (null nếu không lấy được).
    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }

    public string? Title { get; set; }
    public string Level { get; set; } = null!;
    public string InteractionMode { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public string Status { get; set; } = null!;

    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public int? AvgResponseTimeMs { get; set; }
    public int CreditCost { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
