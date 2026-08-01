namespace WebClient.Services.Home;

public interface IHomeFeedbackPopupGate
{
    ValueTask<IDisposable> AcquireAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task StartAuthenticatedLifecycleAsync(
        ISession session,
        Guid userId,
        CancellationToken cancellationToken = default);

    bool PrepareAuthenticatedLifecycle(ISession session, Guid userId);

    bool WasShownFor(ISession session, Guid userId);

    void MarkShown(ISession session, Guid userId);
}

public sealed class HomeFeedbackPopupGate : IHomeFeedbackPopupGate
{
    public const string LifecycleUserKey = "DevPrep:Home:SystemFeedbackPopup:LifecycleUserId";
    public const string ShownUserKey = "DevPrep:Home:SystemFeedbackPopup:ShownUserId";

    private readonly object _sync = new();
    private readonly Dictionary<string, GateEntry> _entries = new(StringComparer.Ordinal);

    public async ValueTask<IDisposable> AcquireAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        GateEntry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(sessionId, out entry!))
            {
                entry = new GateEntry();
                _entries.Add(sessionId, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new GateLease(this, sessionId, entry);
        }
        catch
        {
            ReleaseReference(sessionId, entry);
            throw;
        }
    }

    public async Task StartAuthenticatedLifecycleAsync(
        ISession session,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureValidUserId(userId);

        using var popupLease = await AcquireAsync(session.Id, cancellationToken);
        await session.LoadAsync(cancellationToken);
        session.SetString(LifecycleUserKey, FormatUserId(userId));
        session.Remove(ShownUserKey);
        await session.CommitAsync(cancellationToken);
    }

    public bool PrepareAuthenticatedLifecycle(ISession session, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureValidUserId(userId);

        var formattedUserId = FormatUserId(userId);
        if (string.Equals(
                session.GetString(LifecycleUserKey),
                formattedUserId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        session.SetString(LifecycleUserKey, formattedUserId);
        session.Remove(ShownUserKey);
        return true;
    }

    public bool WasShownFor(ISession session, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureValidUserId(userId);
        return string.Equals(
            session.GetString(ShownUserKey),
            FormatUserId(userId),
            StringComparison.OrdinalIgnoreCase);
    }

    public void MarkShown(ISession session, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureValidUserId(userId);
        session.SetString(ShownUserKey, FormatUserId(userId));
    }

    private void Release(string sessionId, GateEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(sessionId, entry);
    }

    private void ReleaseReference(string sessionId, GateEntry entry)
    {
        lock (_sync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0)
            {
                _entries.Remove(sessionId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private static string FormatUserId(Guid userId) => userId.ToString("N");

    private static void EnsureValidUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An authenticated UserId is required.", nameof(userId));
        }
    }

    private sealed class GateEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class GateLease(
        HomeFeedbackPopupGate owner,
        string sessionId,
        GateEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release(sessionId, entry);
            }
        }
    }
}
