namespace InterviewAPI.DTOs;

public class ManagerInterviewSessionListItemDto
{
    public Guid SessionId { get; set; }
    public Guid? RoomId { get; set; }
    public string? ChannelName { get; set; }
    public string? RoomStatus { get; set; }

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
    public DateTime? InitializedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManagerInterviewSessionLogDto
{
    public Guid SessionId { get; set; }
    public Guid? RoomId { get; set; }
    public string? ChannelName { get; set; }
    public string? RoomStatus { get; set; }

    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }

    public Guid? CvId { get; set; }
    public Guid? JdId { get; set; }
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
    public DateTime? InitializedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<SessionQuestionDto> Questions { get; set; } = new();
}
