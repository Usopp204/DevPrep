namespace WebClient.Models.News;

/// <summary>
/// Dữ liệu trang News (public — cả guest và user đều xem được).
/// </summary>
public class NewsViewModel
{
    public List<NewsListItem> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
    public int TotalPages { get; set; } = 1;
    public string? Error { get; set; }

    public bool HasItems => Items.Count > 0;

    /// <summary>Danh mục distinct để dựng chip lọc (client-side).</summary>
    public IEnumerable<string> Categories =>
        Items.Select(i => i.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c);
}
