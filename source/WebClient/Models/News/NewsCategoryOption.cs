namespace WebClient.Models.News;

/// <summary>Một danh mục để dựng chip lọc trên trang News (khớp NewsCategoryDto của NewsAPI).</summary>
public sealed class NewsCategoryOption
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}
