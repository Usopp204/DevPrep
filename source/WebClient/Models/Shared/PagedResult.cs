using System.Text.Json.Serialization;

namespace WebClient.Models.Shared;

public class PagedResult<T>
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex
    {
        get => Page;
        set => Page = value;
    }

    public int PageSize { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    public int TotalCount
    {
        get => TotalItems;
        set => TotalItems = value;
    }

    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new List<T>();
}