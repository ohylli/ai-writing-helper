using AIWritingHelper.Core;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class OperationLockTests
{
    [Fact]
    public void TryAcquire_FirstCall_ReturnsTrue()
    {
        var lk = new OperationLock();

        Assert.True(lk.TryAcquire());
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeld_ReturnsFalse()
    {
        var lk = new OperationLock();
        lk.TryAcquire();

        Assert.False(lk.TryAcquire());
    }

    [Fact]
    public void TryAcquire_AfterRelease_ReturnsTrue()
    {
        var lk = new OperationLock();
        lk.TryAcquire();
        lk.Release();

        Assert.True(lk.TryAcquire());
    }
}
