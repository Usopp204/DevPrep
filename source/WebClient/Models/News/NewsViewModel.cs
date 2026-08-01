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

    /// <summary>Từ khoá đang lọc (giữ lại để hiện trong ô search và gắn vào các link phân trang/chip).</summary>
    public string? Keyword { get; set; }

    /// <summary>Danh mục đang chọn (null = "All").</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Toàn bộ danh mục đang có bài đã xuất bản — lấy từ server (api/news/categories), KHÔNG suy ra từ Items của trang hiện tại, để chip lọc luôn đủ lựa chọn dù đang search/đứng ở trang nào.</summary>
    public List<NewsCategoryOption> Categories { get; set; } = new();

    public bool HasItems => Items.Count > 0;

    /// <summary>Có đang lọc theo từ khoá và/hoặc danh mục hay không — để chọn đúng thông báo khi danh sách rỗng.</summary>
    public bool IsFiltering => !string.IsNullOrWhiteSpace(Keyword) || CategoryId.HasValue;
}
