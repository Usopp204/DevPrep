namespace InterviewAPI.DTOs.Session;

public class SessionHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Position { get; set; }
    public string? Keyword { get; set; }
}

public static class SessionHistoryQueryNormalizer
{
    public static SessionHistoryQuery Normalize(SessionHistoryQuery? query)
    {
        query ??= new SessionHistoryQuery();

        if (query.Page < 1)
            throw new ArgumentException("Page must be at least 1.");

        if (query.PageSize < 1 || query.PageSize > 50)
            throw new ArgumentException("PageSize must be between 1 and 50.");

        if (query.DateFrom.HasValue && query.DateTo.HasValue && query.DateFrom.Value > query.DateTo.Value)
            throw new ArgumentException("DateFrom must be less than or equal to DateTo.");

        return new SessionHistoryQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Status = NormalizeStatus(query.Status),
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            Position = NormalizeText(query.Position),
            Keyword = NormalizeText(query.Keyword)
        };
    }

    private static string? NormalizeStatus(string? status)
    {
        var trimmed = NormalizeText(status);
        if (trimmed is null)
            return null;

        if (string.Equals(trimmed, "Completed", StringComparison.OrdinalIgnoreCase))
            return "Completed";

        if (string.Equals(trimmed, "Expired", StringComparison.OrdinalIgnoreCase))
            return "Expired";

        if (string.Equals(trimmed, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        throw new ArgumentException("Status must be Completed, Cancelled, or Expired.");
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
