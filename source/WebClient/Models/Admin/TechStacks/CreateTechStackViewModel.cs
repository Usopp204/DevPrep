using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Admin.TechStacks;

public class CreateTechStackViewModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, ErrorMessage = "Name must not exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; set; }
}
