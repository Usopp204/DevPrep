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

    public IAsyncEnumerable<GradingWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public void Complete()
    {
        if (Interlocked.Exchange(ref _accepting, 0) == 1)
            _channel.Writer.TryComplete();
    }
}