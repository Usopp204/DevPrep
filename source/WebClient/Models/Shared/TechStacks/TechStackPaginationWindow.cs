namespace WebClient.Models.Shared.TechStacks;

public sealed record TechStackPaginationWindow(
    IReadOnlyList<int> Pages,
    int? FirstPageShortcut,
    int? LeadingEllipsisTarget,
    int? TrailingEllipsisTarget,
    int? LastPageShortcut,
    bool HasPreviousPage,
    bool HasNextPage)
{
    private const int WindowSize = 5;

    public string? LeadingTooltip => LeadingEllipsisTarget is int page
        ? $"Return to page {page}"
        : null;

    public string? TrailingTooltip => TrailingEllipsisTarget is int page
        ? $"Go to page {page}"
        : null;

    public static TechStackPaginationWindow Create(int pageIndex, int totalPages)
    {
        if (totalPages <= 0)
        {
            return new TechStackPaginationWindow(
                [], null, null, null, null, false, false);
        }

        var currentPage = Math.Clamp(pageIndex, 1, totalPages);
        var windowStart = CalculateWindowStart(currentPage, totalPages);
        var windowEnd = Math.Min(windowStart + WindowSize - 1, totalPages);

        return new TechStackPaginationWindow(
            Enumerable.Range(windowStart, windowEnd - windowStart + 1).ToArray(),
            windowStart > 1 ? 1 : null,
            windowStart > 2 ? windowStart - 1 : null,
            windowEnd < totalPages - 1 ? windowEnd + 1 : null,
            windowEnd < totalPages ? totalPages : null,
            currentPage > 1,
            currentPage < totalPages);
    }

    private static int CalculateWindowStart(int currentPage, int totalPages)
    {
        if (totalPages <= WindowSize || currentPage <= WindowSize)
        {
            return 1;
        }

        var finalWindowStart = totalPages - WindowSize + 1;
        if (currentPage >= finalWindowStart)
        {
            return finalWindowStart;
        }

        return ((currentPage - 1) / WindowSize * WindowSize) + 1;
    }
}
