using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestionAPI.Models;

[Table("Questions")]
public class Question
{
    [Key]
    public Guid QuestionId { get; set; }

    public Guid? CategoryId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(20)]
    public string QuestionType { get; set; } = "Theory";

    [MaxLength(20)]
    public string Level { get; set; } = "Junior";

    [MaxLength(10)]
    public string Difficulty { get; set; } = "Medium";

    public string? ExpectedAnswer { get; set; }

    [MaxLength(255)]
    public string? TechStack { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    // Bổ sung Position
    [MaxLength(50)]
    public string? Position { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }   // <-- THÊM DÒNG NÀY

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public QuestionCategory? Category { get; set; }
}
