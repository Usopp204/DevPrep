namespace InterviewAPI.Services.Grading;

public sealed class GradingQueueOptions
{
    public int Capacity { get; init; } = 256;
    public int WorkerCount { get; init; } = 4;
    public int MaximumAttempts { get; init; } = 3;
    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };
}