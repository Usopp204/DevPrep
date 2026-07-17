using System.ComponentModel.DataAnnotations;

namespace QuestionAPI.DTOs.TechStacks;

public class TechStackQueryDto
{
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 6;

    public string? Search { get; set; }

    public TechStackStatusFilter Status { get; set; } = TechStackStatusFilter.All;
}
