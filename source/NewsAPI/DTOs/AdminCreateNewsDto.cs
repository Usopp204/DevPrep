using System.ComponentModel.DataAnnotations;

namespace NewsAPI.DTOs
{
    public class AdminCreateNewsDto
    {
        public Guid? CategoryId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255)]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Slug is required.")]
        [StringLength(280)]
        public string Slug { get; set; } = null!;

        [StringLength(500)]
        public string? Summary { get; set; }

        [Required(ErrorMessage = "Content is required.")]
        public string Content { get; set; } = null!;

        [StringLength(1024)]
        public string? ThumbnailUrl { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // "Draft" hoặc "Published"
    }
}
