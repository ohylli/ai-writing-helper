using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class DirectInsertionServiceTests
{
    [Fact]
    public async Task HappyPath_OrderIsGetSetPasteRestore()
    {
        var clipboard = new RecordingClipboard { Text = "ORIGINAL" };
        var input = new RecordingInputSimulator();
        // Use a single shared event log to verify exact ordering.
        var log = new List<string>();
        clipboard.Log = log;
        input.Log = log;

        var svc = CreateService(clipboard, input);

        await svc.InsertAsync("hello", CancellationToken.None);

        Assert.Equal(new[] { "get", "set:hello", "paste", "set:ORIGINAL" }, log);
    }

    [Fact]
    public async Task NullOriginal_DoesNotRestore()
    {
        var clipboard = new RecordingClipboard { Text = null };
        var input = new RecordingInputSimulator();
        var log = new List<string>();
        clipboard.Log = log;
        input.Log = log;

        var svc = CreateService(clipboard, input);

        await svc.InsertAsync("hello", CancellationToken.None);

        Assert.Equal(new[] { "get", "set:hello", "paste" }, log);
    }

    [Fact]
    public async Task EmptyOriginal_DoesNotRestore()
    {
        // Clipboard.SetText("") would throw — verify we don't try.
        var clipboard = new RecordingClipboard { Text = "" };
        var input = new RecordingInputSimulator();
        var log = new List<string>();
        clipboard.Log = log;
        input.Log = log;

        var svc = CreateService(clipboard, input);

        await svc.InsertAsync("hello", CancellationToken.None);

        Assert.Equal(new[] { "get", "set:hello", "paste" }, log);
    }

    [Fact]
    public async Task RestoreFailure_IsSwallowed()
    {
        var clipboard = new RecordingClipboard
        {
            Text = "ORIGINAL",
            ThrowOnSetText = (text) => text == "ORIGINAL"
                ? new InvalidOperationException("clipboard locked")
                : null,
        };
        var input = new RecordingInputSimulator();
        var svc = CreateService(clipboard, input);

        // Should not throw.
        await svc.InsertAsync("hello", CancellationToken.None);

        Assert.True(input.PasteCount > 0);
    }

    [Fact]
    public async Task PasteFailure_StillRestoresOriginalClipboard()
    {
        var clipboard = new RecordingClipboard { Text = "ORIGINAL" };
        var input = new RecordingInputSimulator
        {
            ThrowOnPaste = new InvalidOperationException("SendInput blocked"),
        };
        var svc = CreateService(clipboard, input);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.InsertAsync("hello", CancellationToken.None));

        // Even though the paste threw, the finally block should have restored the original.
        Assert.Equal("ORIGINAL", clipboard.Text);
    }

    private static DirectInsertionService CreateService(
        IClipboardService clipboard, IInputSimulator input)
        => new(clipboard, input, NullLoggerFactory.Instance.CreateLogger<DirectInsertionService>());

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; set; }
        public List<string>? Log { get; set; }
        public Func<string, Exception?>? ThrowOnSetText { get; set; }

        public string? GetText()
        {
            Log?.Add("get");
            return Text;
        }

        public void SetText(string text)
        {
            var ex = ThrowOnSetText?.Invoke(text);
            if (ex is not null) throw ex;
            Text = text;
            Log?.Add($"set:{text}");
        }
    }

    private sealed class RecordingInputSimulator : IInputSimulator
    {
        public int PasteCount { get; private set; }
        public List<string>? Log { get; set; }
        public Exception? ThrowOnPaste { get; set; }

        public void SendPaste()
        {
            if (ThrowOnPaste is not null) throw ThrowOnPaste;
            PasteCount++;
            Log?.Add("paste");
        }
    }
}
