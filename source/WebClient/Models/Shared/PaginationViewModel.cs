namespace WebClient.Models.Shared;

public sealed class PaginationViewModel
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public required Func<int, string?> PageUrl { get; init; }
}
