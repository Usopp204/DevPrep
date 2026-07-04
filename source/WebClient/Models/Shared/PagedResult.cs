using System.Text.Json.Serialization;

namespace WebClient.Models.Shared;

public class PagedResult<T>
{
    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new List<T>();
}
