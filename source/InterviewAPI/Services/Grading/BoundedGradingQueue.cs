using System.Threading.Channels;

namespace InterviewAPI.Services.Grading;

public sealed class BoundedGradingQueue : IGradingQueue
{
    private readonly Channel<GradingWorkItem> _channel;
    private readonly ILogger<BoundedGradingQueue> _logger;
    private int _accepting = 1;

    public BoundedGradingQueue(
        GradingQueueOptions options,
        ILogger<BoundedGradingQueue> logger)
    {
        if (options.Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Queue capacity must be positive.");

        _logger = logger;
        _channel = Channel.CreateBounded<GradingWorkItem>(new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public int PendingCount => _channel.Reader.Count;
    public bool IsAccepting => Volatile.Read(ref _accepting) == 1;

    public async ValueTask EnqueueAsync(GradingWorkItem workItem)
    {
        try
        {
            await _channel.Writer.WriteAsync(workItem);
            _logger.LogInformation(
                "Grading work enqueued for session {SessionId}, question {SessionQuestionId}, " +
                "message {ChatMessageId}, user {UserId}.",
                workItem.SessionId,
                workItem.SessionQuestionId,
                workItem.ChatMessageId,
                workItem.UserId);
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning(
                "Grading enqueue rejected during application shutdown for session {SessionId}, " +
                "question {SessionQuestionId}, message {ChatMessageId}, user {UserId}.",
                workItem.SessionId,
                workItem.SessionQuestionId,
                workItem.ChatMessageId,
                workItem.UserId);
            throw;
        }
    }

    /// <summary>
    /// Non-blocking counterpart to EnqueueAsync — see IGradingQueue for why both exist and why they
    /// must not be merged. TryWrite returns false when the bounded channel is full (the writer is
    /// configured FullMode.Wait, which only affects the awaiting WriteAsync path) or once the
    /// channel has been completed at shutdown.
    /// </summary>
    public bool TryEnqueue(GradingWorkItem workItem)
    {
        if (_channel.Writer.TryWrite(workItem))
        {
            _logger.LogInformation(
                "Grading work re-enqueued for session {SessionId}, question {SessionQuestionId}, " +
                "message {ChatMessageId}, user {UserId}.",
                workItem.SessionId,
                workItem.SessionQuestionId,
                workItem.ChatMessageId,
                workItem.UserId);
            return true;
        }

        // Warning, not Debug: a dropped recovery attempt is exactly the silent-loss class of bug
        // this recovery path exists to fix, so it must be findable in the logs.
        _logger.LogWarning(
            "Grading work could NOT be re-enqueued (queue full or shutting down; pending {PendingCount}) " +
            "for session {SessionId}, question {SessionQuestionId}, message {ChatMessageId}, user {UserId}. " +
            "The answer stays ungraded until the next recovery attempt.",
            PendingCount,
            workItem.SessionId,
            workItem.SessionQuestionId,
            workItem.ChatMessageId,
            workItem.UserId);
        return false;
    }

    public IAsyncEnumerable<GradingWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public void Complete()
    {
        if (Interlocked.Exchange(ref _accepting, 0) == 1)
            _channel.Writer.TryComplete();
    }
}