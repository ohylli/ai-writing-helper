namespace AIWritingHelper.Core;

public class OperationLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Returns true if lock acquired, false if already held.
    /// Non-blocking — returns immediately.
    /// </summary>
    public bool TryAcquire() => _semaphore.Wait(0);

    public void Release() => _semaphore.Release();
}
