namespace QuestionAPI.DTOs;

public class AdminQuestionDetailDto
{
    public Guid QuestionId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Category { get; set; }

    public string Question { get; set; } = string.Empty;

    // xóa 2 trường 
    //public string QuestionType { get; set; } = string.Empty;

    //public string Level { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string? Answer { get; set; } // thay đổi tên 

    // xóa 2 trường 
    //public string? TechStack { get; set; }

    //public string? Tags { get; set; }
    // Bổ sung
    public string? Position { get; set; }


    public string Status { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}