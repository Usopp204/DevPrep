namespace NewsAPI.DTOs;

public class AdminNewsQueryDto
{
    public string? Keyword { get; set; }
    public string? Status { get; set; } // "Draft" hoặc "Published"
    public Guid? CategoryId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}