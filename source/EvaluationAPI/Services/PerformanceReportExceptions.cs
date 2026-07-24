namespace EvaluationAPI.Services;

public sealed class ReportNotFoundException : Exception
{
    public ReportNotFoundException() : base("No report found for this session.") { }
}

public sealed class SessionNotEligibleForPerformanceReportException : Exception
{
    public SessionNotEligibleForPerformanceReportException(string status)
        : base($"Session status '{status}' is not eligible for a performance report.")
    {
        Status = status;
    }

    public string Status { get; }
}