namespace InterviewAPI.Services.Grading;

public interface IGradingQueue
{
    int PendingCount { get; }
    bool IsAccepting { get; }
    ValueTask EnqueueAsync(GradingWorkItem workItem);
    IAsyncEnumerable<GradingWorkItem> ReadAllAsync(CancellationToken cancellationToken);
    void Complete();
}