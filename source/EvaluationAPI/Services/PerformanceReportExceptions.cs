namespace EvaluationAPI.Services;

public sealed class SessionNotEligibleForPerformanceReportException : Exception
{
    public SessionNotEligibleForPerformanceReportException(string status)
        : base($"Session status '{status}' is not eligible for a performance report.")
    {
    }
}