namespace NewsAPI.DTOs;

/// <summary>Tham số lọc + phân trang cho danh sách tin công khai (guest/candidate).</summary>
public class NewsQueryDto
{
    /// <summary>Từ khoá tìm trong Title và Summary (không phân biệt hoa thường/dấu).</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo danh mục.</summary>
    public Guid? CategoryId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}
