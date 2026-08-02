namespace StatisticsAPI.Services;

public sealed class StatisticsCollectionGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable?> EnterAsync(bool waitForAvailability, CancellationToken cancellationToken)
    {
        if (waitForAvailability)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new Lease(_semaphore);
        }

        return await _semaphore.WaitAsync(0, cancellationToken)
            ? new Lease(_semaphore)
            : null;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}