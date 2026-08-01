namespace NewsAPI.DTOs;

/// <summary>Một danh mục có ít nhất một bài đã xuất bản (dùng cho chip lọc công khai).</summary>
public class NewsCategoryDto
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
}
