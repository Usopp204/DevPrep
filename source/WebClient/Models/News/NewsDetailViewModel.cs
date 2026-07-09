namespace WebClient.Models.News;

/// <summary>
/// Dữ liệu trang chi tiết một bài News (public). Found=false khi không tìm thấy/lỗi.
/// </summary>
public class NewsDetailViewModel
{
    public bool Found { get; set; }
    public string? Error { get; set; }

    public Guid NewsId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Vài bài liên quan (lấy từ danh sách, loại trừ bài hiện tại).</summary>
    public List<NewsListItem> Related { get; set; } = new();

    public DateTime DisplayDate => PublishedAt ?? CreatedAt;
    public string Category => string.IsNullOrWhiteSpace(CategoryName) ? "General" : CategoryName!;
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);
    public string Initial => string.IsNullOrWhiteSpace(Title) ? "N" : Title.Trim()[..1].ToUpperInvariant();

    /// <summary>Ước lượng thời gian đọc (~900 ký tự/phút).</summary>
    public int ReadMinutes => Math.Max(1, (int)Math.Ceiling((Content?.Length ?? 0) / 900.0));
}
