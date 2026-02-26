namespace AIWritingHelper.Core;

public class OperationLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Returns true if lock acquired, false if already held.
    /// Non-blocking — returns immediately.
    /// </summary>
    public bool TryAcquire() => _semaphore.Wait(0);

    /// <summary>
    /// Releases the lock. Idempotent — safe to call even if not currently held.
    /// </summary>
    public void Release()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already released — ignore to make Release() idempotent.
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
