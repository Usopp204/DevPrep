namespace QuestionAPI.DTOs;

public class AdminPagedResultDto<T>
{
    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalRecords { get; set; }

    public int TotalPages { get; set; }

    public List<T> Items { get; set; } = new();
}