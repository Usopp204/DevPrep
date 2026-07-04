namespace NewsAPI.DTOs;

public class ManagerNewsListItemDto
{
    public Guid NewsId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CategoryName { get; set; }
    public string Status { get; set; } = null!;
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ManagerNewsDetailDto : ManagerNewsListItemDto
{
    public string Content { get; set; } = null!;
    public Guid AuthorId { get; set; }
}
