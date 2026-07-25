using System.ComponentModel.DataAnnotations;

namespace QuestionAPI.DTOs;

public class AdminUpdateQuestionDto
{
    public Guid? CategoryId { get; set; }

    public string? Question { get; set; }

    //public string? QuestionType { get; set; }

    //public string? Level { get; set; }

    public string? Difficulty { get; set; }

    public string? Answer { get; set; }

    //public string? TechStack { get; set; }

    //public string? Tags { get; set; }
    // Bổ sung
    //[StringLength(
    //    50,
    //    ErrorMessage = "Position cannot exceed 50 characters."
    //)]
    // public string? Position { get; set; }
    public Guid? PositionId { get; set; }
    public string? Status { get; set; }
}