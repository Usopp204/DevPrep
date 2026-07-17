using System.ComponentModel.DataAnnotations;

namespace QuestionAPI.DTOs.TechStacks;

public class UpdateTechStackDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public bool? IsActive { get; set; }
}
