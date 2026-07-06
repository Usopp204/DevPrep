namespace WebClient.Models.News;

/// <summary>Khớp NewsDetailDto của NewsAPI (GET /api/news/{id}).</summary>
public sealed class NewsDetailData
{
    public Guid NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
