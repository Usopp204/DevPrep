using InterviewAPI.Clients;

namespace InterviewAPI.Services.Grading;

public sealed class GradingBackgroundService : BackgroundService
{
    private readonly IGradingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GradingQueueOptions _options;
    private readonly ILogger<GradingBackgroundService> _logger;
    private readonly CancellationTokenSource _forceStop = new();
    private int _activeCount;
    private int _incompleteShutdownCount;

    public GradingBackgroundService(
        IGradingQueue queue,
        IServiceScopeFactory scopeFactory,
        GradingQueueOptions options,
        ILogger<GradingBackgroundService> logger)
    {
        if (options.WorkerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Worker count must be positive.");
        if (options.MaximumAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum attempts must be positive.");
        if (options.RetryDelays.Count < options.MaximumAttempts - 1)
            throw new ArgumentException("A retry delay is required before every retry attempt.", nameof(options));

        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public int ActiveCount => Volatile.Read(ref _activeCount);
    public int IncompleteShutdownCount => Volatile.Read(ref _incompleteShutdownCount);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            _forceStop.Token);
        var workers = Enumerable.Range(1, _options.WorkerCount)
            .Select(workerId => WorkerLoopAsync(workerId, linkedCancellation.Token))
            .ToArray();

        await Task.WhenAll(workers);
        _logger.LogInformation(
            "Grading queue stopped. Pending {PendingCount}; Active {ActiveCount}; " +
            "AcceptedIncomplete {IncompleteCount}.",
            _queue.PendingCount,
            ActiveCount,
            IncompleteShutdownCount);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        var executeTask = ExecuteTask;
        if (executeTask is null)
            return;

        try
        {
            await executeTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _forceStop.Cancel();
            _logger.LogWarning(
                "Grading shutdown deadline reached. Pending {PendingCount}; Active {ActiveCount}.",
                _queue.PendingCount,
                ActiveCount);

            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Worker cancellation is observed here and accounted for by each worker.
            }
        }
    }

    public override void Dispose()
    {
        _forceStop.Cancel();
        _forceStop.Dispose();
        base.Dispose();
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var workItem in _queue.ReadAllAsync(cancellationToken))
            {
                Interlocked.Increment(ref _activeCount);
                var completed = false;
                try
                {
                    completed = await ProcessWorkItemAsync(workerId, workItem, cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeCount);
                    if (!completed)
                        Interlocked.Increment(ref _incompleteShutdownCount);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Grading worker {WorkerId} stopped during application shutdown.", workerId);
        }
    }

    private async Task<bool> ProcessWorkItemAsync(
        int workerId,
        GradingWorkItem workItem,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaximumAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation(
                    "Grading attempt {Attempt}/{MaximumAttempts} started by worker {WorkerId} for " +
                    "session {SessionId}, question {SessionQuestionId}, message {ChatMessageId}, user {UserId}.",
                    attempt,
                    _options.MaximumAttempts,
                    workerId,
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId);

                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IEvaluationApiClient>();
                var result = await client.EvaluateAnswerAsync(
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.Payload,
                    workItem.BearerToken,
                    cancellationToken);

                _logger.LogInformation(
                    "Grading attempt succeeded for session {SessionId}, question {SessionQuestionId}, " +
                    "message {ChatMessageId}, user {UserId}, evaluation {EvaluationId}, attempt {Attempt}/{MaximumAttempts}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    result.EvaluationId,
                    attempt,
                    _options.MaximumAttempts);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Grading work cancelled during application shutdown for session {SessionId}, " +
                    "question {SessionQuestionId}, message {ChatMessageId}, user {UserId}, " +
                    "attempt {Attempt}/{MaximumAttempts}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    attempt,
                    _options.MaximumAttempts);
                return false;
            }
            catch (EvaluationApiCallException ex) when (!ex.IsRetryable)
            {
                _logger.LogWarning(
                    "Non-retryable grading failure for session {SessionId}, question {SessionQuestionId}, " +
                    "message {ChatMessageId}, user {UserId}, attempt {Attempt}/{MaximumAttempts}. " +
                    "Category {FailureCategory}; StatusCode {StatusCode}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    attempt,
                    _options.MaximumAttempts,
                    ex.Category,
                    ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : null);
                return true;
            }
            catch (EvaluationApiCallException ex) when (attempt == _options.MaximumAttempts)
            {
                _logger.LogWarning(
                    "Grading retries exhausted for session {SessionId}, question {SessionQuestionId}, " +
                    "message {ChatMessageId}, user {UserId}, attempt {Attempt}/{MaximumAttempts}. " +
                    "Category {FailureCategory}; StatusCode {StatusCode}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    attempt,
                    _options.MaximumAttempts,
                    ex.Category,
                    ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : null);
                return true;
            }
            catch (EvaluationApiCallException ex)
            {
                _logger.LogWarning(
                    "Retryable grading attempt failed for session {SessionId}, question {SessionQuestionId}, " +
                    "message {ChatMessageId}, user {UserId}, attempt {Attempt}/{MaximumAttempts}. " +
                    "Category {FailureCategory}; StatusCode {StatusCode}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    attempt,
                    _options.MaximumAttempts,
                    ex.Category,
                    ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : null);

                var delay = _options.RetryDelays[attempt - 1];
                _logger.LogInformation(
                    "Grading retry scheduled after {RetryDelayMs}ms for session {SessionId}, " +
                    "question {SessionQuestionId}, message {ChatMessageId}, next attempt {NextAttempt}/{MaximumAttempts}.",
                    delay.TotalMilliseconds,
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    attempt + 1,
                    _options.MaximumAttempts);
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Grading retry cancelled during application shutdown for session {SessionId}, " +
                        "question {SessionQuestionId}, message {ChatMessageId}, user {UserId}.",
                        workItem.SessionId,
                        workItem.SessionQuestionId,
                        workItem.ChatMessageId,
                        workItem.UserId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected non-retryable grading worker failure for session {SessionId}, " +
                    "question {SessionQuestionId}, message {ChatMessageId}, user {UserId}, " +
                    "attempt {Attempt}/{MaximumAttempts}.",
                    workItem.SessionId,
                    workItem.SessionQuestionId,
                    workItem.ChatMessageId,
                    workItem.UserId,
                    attempt,
                    _options.MaximumAttempts);
                return true;
            }
        }

        return true;
    }
}