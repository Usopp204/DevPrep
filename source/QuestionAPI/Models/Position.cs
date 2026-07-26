using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestionAPI.Models;

/// <summary>
/// Danh mục Position (Backend, Frontend...) — nguồn cho dropdown phân loại câu hỏi.
/// Soft-delete bằng IsDeleted (unique tên chỉ áp cho dòng chưa xoá — filtered index).
/// </summary>
[Table("Positions")]
public class Position
{
    [Key]
    public Guid PositionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
