namespace WebClient.Models.Dashboard;

public sealed class ManagementDashboardViewModel
{
    public ManagementDashboardData Dashboard { get; set; } = new();
    public string DashboardDataUrl { get; set; } = string.Empty;
}

public sealed class ManagementDashboardData
{
    public bool Available { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Granularity { get; set; } = "Daily";

    public long? TotalCandidatesAsOf { get; set; }
    public long? NewCandidates { get; set; }
    public long? TotalInterviewSessionsAsOf { get; set; }
    public long? CompletedInterviewSessionsAsOf { get; set; }
    public long? InterviewSessionsCreated { get; set; }
    public long? InterviewSessionsCompleted { get; set; }
    public long? TotalSystemFeedbacksAsOf { get; set; }
    public long? TotalInterviewFeedbacksAsOf { get; set; }
    public long? TotalFeedbacksAsOf { get; set; }
    public long? NewSystemFeedbacks { get; set; }
    public long? NewInterviewFeedbacks { get; set; }
    public long? NewFeedbacks { get; set; }
    public decimal? GrossVnpayRevenueAsOf { get; set; }
    public long? PaidVnpayOrdersAsOf { get; set; }
    public decimal? PeriodGrossVnpayRevenue { get; set; }
    public long? PeriodPaidVnpayOrders { get; set; }
    public List<ManagementDashboardBucket> Buckets { get; set; } = new();

    public static ManagementDashboardData Unavailable(DateOnly from, DateOnly to) => new()
    {
        From = from,
        To = to,
        Granularity = DashboardDateRange.Granularity(to.DayNumber - from.DayNumber + 1)
    };
}

public sealed class ManagementDashboardBucket
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
    public long NewCandidates { get; set; }
    public long InterviewSessionsCreated { get; set; }
    public long InterviewSessionsCompleted { get; set; }
    public decimal GrossVnpayRevenue { get; set; }
    public long PaidVnpayOrders { get; set; }
    public long SystemFeedbacks { get; set; }
    public long InterviewFeedbacks { get; set; }
    public long TotalFeedbacks { get; set; }
}

public static class DashboardDateRange
{
    public const string OrderError = "The From date must be on or before the To date.";
    public const string LengthError = "The selected date range must contain no more than 366 days.";

    public static bool TryResolve(
        DateOnly? from,
        DateOnly? to,
        DateOnly today,
        out DateOnly resolvedFrom,
        out DateOnly resolvedTo,
        out string? error)
    {
        resolvedTo = to ?? today;
        resolvedFrom = from ?? new DateOnly(resolvedTo.Year, resolvedTo.Month, 1);

        if (resolvedFrom > resolvedTo)
        {
            error = OrderError;
            return false;
        }

        if (resolvedTo.DayNumber - resolvedFrom.DayNumber + 1 > 366)
        {
            error = LengthError;
            return false;
        }

        error = null;
        return true;
    }

    public static string Granularity(int inclusiveDays)
    {
        if (inclusiveDays <= 31)
            return "Daily";
        if (inclusiveDays <= 180)
            return "Weekly";
        return "Monthly";
    }
}
