using WebClient.Models.Shared.TechStacks;

namespace WebClient.Models.Admin.TechStacks;

public class AdminTechStackPagedResponse
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public List<TechStackListItemViewModel> Items { get; set; } = new();
}