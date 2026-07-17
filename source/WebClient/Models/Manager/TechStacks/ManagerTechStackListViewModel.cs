using WebClient.Models.Shared.TechStacks;

namespace WebClient.Models.Manager.TechStacks;

public sealed class ManagerTechStackListViewModel
{
    public IReadOnlyList<TechStackListItemViewModel> Items { get; set; } =
        Array.Empty<TechStackListItemViewModel>();

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 6;
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public string? ApiErrorMessage { get; set; }
    public bool HasApiError => !string.IsNullOrWhiteSpace(ApiErrorMessage);
    public TechStackPaginationWindow Pagination =>
        TechStackPaginationWindow.Create(PageIndex, TotalPages);
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) ||
        !string.Equals(Status, "All", StringComparison.OrdinalIgnoreCase);
}