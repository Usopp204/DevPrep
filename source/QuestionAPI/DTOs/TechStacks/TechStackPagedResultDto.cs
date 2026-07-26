namespace QuestionAPI.DTOs.TechStacks;

public class TechStackPagedResultDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyList<TechStackListItemDto> Items { get; set; } =
        Array.Empty<TechStackListItemDto>();
}
