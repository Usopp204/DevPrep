namespace WebClient.Models.Shared;

public sealed record PaginationWindow(
    IReadOnlyList<int> Pages,
    int? FirstPage,
    bool HasLeadingEllipsis,
    bool HasTrailingEllipsis,
    int? LastPage,
    bool HasPreviousPage,
    bool HasNextPage)
{
    private const int WindowSize = 5;

    public static PaginationWindow Create(int currentPage, int totalPages)
    {
        if (totalPages <= 0)
        {
            return new PaginationWindow([], null, false, false, null, false, false);
        }

        var page = Math.Clamp(currentPage, 1, totalPages);
        var windowStart = Math.Max(1, page - (WindowSize / 2));
        var windowEnd = Math.Min(totalPages, windowStart + WindowSize - 1);
        windowStart = Math.Max(1, windowEnd - WindowSize + 1);

        return new PaginationWindow(
            Enumerable.Range(windowStart, windowEnd - windowStart + 1).ToArray(),
            windowStart > 1 ? 1 : null,
            windowStart > 2,
            windowEnd < totalPages - 1,
            windowEnd < totalPages ? totalPages : null,
            page > 1,
            page < totalPages);
    }
}
