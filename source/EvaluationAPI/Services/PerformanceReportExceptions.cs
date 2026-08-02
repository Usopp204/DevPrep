namespace EvaluationAPI.Services;

public sealed class AggregateReportNotReadyException : Exception
{
    public AggregateReportNotReadyException(string state)
        : base($"Aggregate report generation is not ready while evaluation state is '{state}'." )
    {
        State = state;
    }

    public string State { get; }
}

public sealed class SessionNotEligibleForPerformanceReportException : Exception
{
    public SessionNotEligibleForPerformanceReportException(string status)
        : base($"Session status '{status}' is not eligible for a performance report.")
    {
    }
}