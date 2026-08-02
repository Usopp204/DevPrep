namespace StatisticsAPI.Helpers;

public readonly record struct DashboardDateRangeSelection(DateOnly From, DateOnly To)
{
    public int InclusiveDays => To.DayNumber - From.DayNumber + 1;
}

public static class DashboardDateRange
{
    public const int MaximumInclusiveDays = 366;
    public const string OrderError = "The From date must be on or before the To date.";
    public const string LengthError = "The selected date range must contain no more than 366 days.";

    public static bool TryResolve(
        DateOnly? from,
        DateOnly? to,
        DateOnly today,
        out DashboardDateRangeSelection range,
        out string? error)
    {
        var resolvedTo = to ?? today;
        var resolvedFrom = from ?? new DateOnly(resolvedTo.Year, resolvedTo.Month, 1);
        range = new DashboardDateRangeSelection(resolvedFrom, resolvedTo);

        if (resolvedFrom > resolvedTo)
        {
            error = OrderError;
            return false;
        }

        if (range.InclusiveDays > MaximumInclusiveDays)
        {
            error = LengthError;
            return false;
        }

        error = null;
        return true;
    }
}