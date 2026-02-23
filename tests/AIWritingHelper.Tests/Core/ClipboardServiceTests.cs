using AIWritingHelper.Core;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class ClipboardServiceTests
{
    private readonly ClipboardService _service = new();

    [StaFact]
    public void GetText_EmptyClipboard_ReturnsNull()
    {
        Clipboard.Clear();
        var result = _service.GetText();
        Assert.Null(result);
    }

    [StaFact]
    public void SetText_GetText_RoundTrip()
    {
        const string expected = "Hello, clipboard!";
        _service.SetText(expected);
        var result = _service.GetText();
        Assert.Equal(expected, result);
    }

    [StaFact]
    public void SetText_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.SetText(null!));
    }

    [StaFact]
    public void GetText_NonTextClipboard_ReturnsNull()
    {
        Clipboard.Clear();
        Clipboard.SetImage(new Bitmap(1, 1));
        var result = _service.GetText();
        Assert.Null(result);
    }

    [Fact]
    public async Task SetText_GetText_RoundTrip_FromMtaThread()
    {
        const string expected = "Hello from MTA!";
        await Task.Run(() =>
        {
            Assert.NotEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            _service.SetText(expected);
            var result = _service.GetText();
            Assert.Equal(expected, result);
        });
    }

    [Fact]
    public async Task SetText_Null_ThrowsArgumentNullException_FromMtaThread()
    {
        await Task.Run(() =>
        {
            Assert.NotEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            Assert.Throws<ArgumentNullException>(() => _service.SetText(null!));
        });
    }
}
