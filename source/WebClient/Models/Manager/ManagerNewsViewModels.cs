using System.Text.Json.Serialization;

namespace WebClient.Models.Manager;

public class ManagerNewsListItemViewModel
{
    public Guid NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CategoryName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ManagerNewsDetailViewModel : ManagerNewsListItemViewModel
{
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
}

public class ManagerNewsPagedResult<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("page")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}
