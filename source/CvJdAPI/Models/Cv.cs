namespace CvJdAPI.Models;

public class Cv
{
    public Guid CvId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = "pdf";
    public int? FileSizeKb { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string ParseStatus { get; set; } = "Pending";
    public string? ParsedJson { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
