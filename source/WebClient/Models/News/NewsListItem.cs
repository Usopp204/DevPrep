namespace WebClient.Models.News;

/// <summary>
/// Một bài tin trong danh sách, khớp NewsListItemDto của NewsAPI
/// (newsId/title/slug/summary/thumbnailUrl/categoryName/viewCount/publishedAt/createdAt).
/// </summary>
public sealed class NewsListItem
{
    public Guid NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime DisplayDate => PublishedAt ?? CreatedAt;
    public string Category => string.IsNullOrWhiteSpace(CategoryName) ? "General" : CategoryName!;
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);

    /// <summary>Chữ cái đầu tiêu đề để làm ảnh placeholder khi không có thumbnail.</summary>
    public string Initial => string.IsNullOrWhiteSpace(Title) ? "N" : Title.Trim()[..1].ToUpperInvariant();
}
