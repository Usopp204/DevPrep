namespace NewsAPI.DTOs;

/// <summary>Một dòng trong danh sách tin tức (không kèm nội dung đầy đủ).</summary>
public class NewsListItemDto
{
    public Guid NewsId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
