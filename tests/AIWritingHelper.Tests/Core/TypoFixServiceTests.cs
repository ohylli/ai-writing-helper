using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class TypoFixServiceTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeLLMProvider _llm = new();
    private readonly FakeSoundPlayer _sound = new();
    private readonly FakeTrayNotifier _notifier = new();
    private readonly AppSettings _settings = new();
    private readonly OperationLock _lock = new();

    private TypoFixService CreateService() => new(
        _clipboard, _llm, _sound, _notifier, _settings, _lock,
        NullLoggerFactory.Instance.CreateLogger<TypoFixService>());

    [Fact]
    public async Task HappyPath_FixesTextAndPlaysSuccess()
    {
        _clipboard.Text = "hello wrold";
        _llm.Result = "hello world";
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.Equal("hello world", _clipboard.Text);
        Assert.True(_sound.SuccessPlayed);
        Assert.False(_sound.ErrorPlayed);
    }

    [Fact]
    public async Task EmptyClipboard_PlaysErrorAndNotifies()
    {
        _clipboard.Text = null;
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Contains("No text on clipboard", _notifier.LastErrorMessage);
        Assert.False(_llm.WasCalled);
    }

    [Fact]
    public async Task Busy_PlaysErrorAndDoesNothing()
    {
        _lock.TryAcquire(); // hold the lock
        _clipboard.Text = "some text";
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.False(_llm.WasCalled);
        Assert.Null(_notifier.LastErrorMessage);
    }

    [Fact]
    public async Task HttpRequestException_PlaysErrorAndNotifies()
    {
        _clipboard.Text = "some text";
        _llm.ExceptionToThrow = new HttpRequestException("connection refused");
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Contains("Could not reach", _notifier.LastErrorMessage);
    }

    [Fact]
    public async Task TimeoutException_PlaysErrorAndNotifies()
    {
        _clipboard.Text = "some text";
        _llm.ExceptionToThrow = new TimeoutException("timed out");
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Contains("timed out", _notifier.LastErrorMessage);
    }

    [Fact]
    public async Task OperationCancelled_PlaysErrorButNoNotification()
    {
        _clipboard.Text = "some text";
        _llm.ExceptionToThrow = new OperationCanceledException();
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Null(_notifier.LastErrorMessage);
    }

    [Fact]
    public async Task LockReleasedAfterError()
    {
        _clipboard.Text = "some text";
        _llm.ExceptionToThrow = new HttpRequestException("fail");
        var svc = CreateService();

        await svc.ExecuteAsync(CancellationToken.None);

        // Lock should be released — we can acquire it again
        Assert.True(_lock.TryAcquire());
    }

    // --- Fakes ---

    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; set; }
        public string? GetText() => Text;
        public void SetText(string text) => Text = text;
    }

    private sealed class FakeLLMProvider : ILLMProvider
    {
        public string Result { get; set; } = "fixed";
        public Exception? ExceptionToThrow { get; set; }
        public bool WasCalled { get; private set; }

        public Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct)
        {
            WasCalled = true;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(Result);
        }

        public Task<string> FixTextAsync(string text, string systemPrompt, string apiEndpoint, string apiKey, string modelName, CancellationToken ct)
            => FixTextAsync(text, systemPrompt, ct);
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public bool SuccessPlayed { get; private set; }
        public bool ErrorPlayed { get; private set; }
        public void PlaySuccess() => SuccessPlayed = true;
        public void PlayError() => ErrorPlayed = true;
        public void PlayRecordingStart() { }
        public void PlayRecordingStop() { }
    }

    private sealed class FakeTrayNotifier : ITrayNotifier
    {
        public string? LastErrorMessage { get; private set; }
        public void ShowNotification(string title, string message) { }
        public void ShowError(string title, string message) => LastErrorMessage = message;
    }
}
