using System.ComponentModel.DataAnnotations;

namespace QuestionAPI.DTOs;

public class AdminCreateQuestionDto
{
    [Required(ErrorMessage = "CategoryId is required.")]
    public Guid? CategoryId { get; set; }

    [Required(ErrorMessage = "Question content is required.")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Question type is required.")]
    [StringLength(20)]
    public string QuestionType { get; set; } = "Theory";

    [Required(ErrorMessage = "Level is required.")]
    [StringLength(20)]
    public string Level { get; set; } = "Junior";

    [Required(ErrorMessage = "Difficulty is required.")]
    [StringLength(10)]
    public string Difficulty { get; set; } = "Medium";

    public string? ExpectedAnswer { get; set; }

    [StringLength(255)]
    public string? TechStack { get; set; }

    [StringLength(500)]
    public string? Tags { get; set; }
    // Bổ sung
    [StringLength(
        50,
        ErrorMessage = "Position cannot exceed 50 characters."
    )]
    public string? Position { get; set; }
}