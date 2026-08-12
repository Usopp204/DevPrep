namespace InterviewAPI.Services.Grading;

public interface IGradingQueue
{
    int PendingCount { get; }
    bool IsAccepting { get; }

    /// <summary>
    /// BLOCKING enqueue — waits for a slot when the queue is full. Use this from the
    /// answer-submission path ONLY. There the answer has already been persisted and its grading
    /// work is the only copy: dropping it is real data loss, so waiting is the correct trade.
    /// </summary>
    ValueTask EnqueueAsync(GradingWorkItem workItem);

    /// <summary>
    /// NON-BLOCKING enqueue — returns false immediately if the queue is full or closed, instead of
    /// waiting for a slot. Use this for RECOVERY work (re-grading answers whose original job was
    /// already lost), where the caller is serving a live HTTP request and must not be stalled by a
    /// backed-up queue, and where dropping is safe because the same recovery can simply be
    /// triggered again later.
    ///
    /// Do NOT "simplify" the two paths into one call. They differ on purpose:
    ///   EnqueueAsync -> the work is irreplaceable, waiting is correct.
    ///   TryEnqueue   -> the work is a retry of something already lost, dropping is correct.
    /// Making the answer path non-blocking would silently lose gradings under load; making the
    /// recovery path blocking would let a full queue stall request threads.
    /// </summary>
    bool TryEnqueue(GradingWorkItem workItem);
    IAsyncEnumerable<GradingWorkItem> ReadAllAsync(CancellationToken cancellationToken);
    void Complete();
}