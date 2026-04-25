using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class DictationServiceTests
{
    private readonly FakeAudioRecorder _recorder = new();
    private readonly FakeSttProvider _stt = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSoundPlayer _sound = new();
    private readonly FakeTrayNotifier _notifier = new();
    private readonly AppSettings _settings = new();
    private readonly OperationLock _lock = new();

    private DictationService CreateService() => new(
        _recorder, _stt, _clipboard, _sound, _notifier, _settings, _lock,
        NullLoggerFactory.Instance.CreateLogger<DictationService>());

    [Fact]
    public async Task FirstToggle_AcquiresLockAndStartsRecording()
    {
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_recorder.StartCalled);
        Assert.True(_sound.RecordingStartPlayed);
        Assert.False(_sound.SuccessPlayed);
        Assert.False(_sound.ErrorPlayed);
        // Lock should be held by the dictation service.
        Assert.False(_lock.TryAcquire());
    }

    [Fact]
    public async Task SecondToggle_TranscribesAndWritesClipboard()
    {
        _stt.Result = "hello world";
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_recorder.StopCalled);
        Assert.True(_stt.WasCalled);
        Assert.Equal("hello world", _clipboard.Text);
        Assert.True(_sound.RecordingStopPlayed);
        Assert.True(_sound.SuccessPlayed);
        // Lock released after success.
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task SecondToggle_EmptyTranscript_DoesNotClobberClipboard()
    {
        _clipboard.Text = "previous";
        _stt.Result = "";
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.Equal("previous", _clipboard.Text);
        Assert.True(_sound.ErrorPlayed);
        Assert.False(_sound.SuccessPlayed);
        Assert.NotNull(_notifier.LastInfoMessage);
        Assert.Contains("No speech", _notifier.LastInfoMessage);
        Assert.Null(_notifier.LastErrorMessage);
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task Busy_FirstToggleRejected()
    {
        _lock.TryAcquire(); // pre-hold the lock
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);

        Assert.False(_recorder.StartCalled);
        Assert.True(_sound.ErrorPlayed);
        Assert.Null(_notifier.LastErrorMessage);
        Assert.False(_sound.RecordingStartPlayed);
    }

    [Fact]
    public async Task RecorderStartThrows_ReleasesLockAndNotifies()
    {
        _recorder.StartException = new InvalidOperationException("device gone");
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.NotNull(_notifier.LastErrorMessage);
        Assert.Contains("microphone", _notifier.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Lock released — we can re-acquire.
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task SttHttpError_NotifiesAndReleasesLock()
    {
        _stt.ExceptionToThrow = new HttpRequestException("connection refused");
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Contains("Could not reach", _notifier.LastErrorMessage);
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task SttTimeout_NotifiesAndReleasesLock()
    {
        _stt.ExceptionToThrow = new TimeoutException("timed out");
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Contains("timed out", _notifier.LastErrorMessage);
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task SttCancelled_PlaysErrorButNoNotification()
    {
        _stt.ExceptionToThrow = new OperationCanceledException();
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.True(_sound.ErrorPlayed);
        Assert.Null(_notifier.LastErrorMessage);
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task SttInvalidOperation_SurfacesProviderMessage()
    {
        _stt.ExceptionToThrow = new InvalidOperationException("STT API key is missing");
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.Equal("STT API key is missing", _notifier.LastErrorMessage);
        Assert.True(_lock.TryAcquire());
    }

    [Fact]
    public async Task RecordingFault_DuringRecording_ReleasesLock()
    {
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        Assert.False(_lock.TryAcquire()); // held during recording

        _recorder.TriggerFault(new InvalidOperationException("device disconnected"));

        Assert.True(_sound.ErrorPlayed);
        Assert.NotNull(_notifier.LastErrorMessage);
        Assert.Contains("Microphone", _notifier.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Lock released by the fault handler.
        Assert.True(_lock.TryAcquire());
        // After releasing the lock manually, a fresh toggle should be able to start again.
        _lock.Release();
        _recorder.Reset();
        _sound.Reset();
        await svc.ToggleAsync(CancellationToken.None);
        Assert.True(_recorder.StartCalled);
    }

    [Fact]
    public async Task MicDeviceName_PassedFromSettings()
    {
        _settings.MicrophoneDeviceName = "USB Mic";
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);

        Assert.Equal("USB Mic", _recorder.LastDeviceName);
    }

    [Fact]
    public async Task MicDeviceName_EmptyMappedToNull()
    {
        _settings.MicrophoneDeviceName = "";
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);

        Assert.Null(_recorder.LastDeviceName);
    }

    [Fact]
    public async Task SttReceivesAudioStreamFromRecorder()
    {
        _stt.Result = "ok";
        var svc = CreateService();

        await svc.ToggleAsync(CancellationToken.None);
        await svc.ToggleAsync(CancellationToken.None);

        Assert.NotNull(_stt.LastAudio);
    }

    // --- Fakes ---

    private sealed class FakeAudioRecorder : IAudioRecorder
    {
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public string? LastDeviceName { get; private set; }
        public Exception? StartException { get; set; }
        public bool IsRecording { get; private set; }

        public event Action<Exception>? RecordingFaulted;

        public void Start(string? deviceName)
        {
            LastDeviceName = deviceName;
            if (StartException is not null) throw StartException;
            StartCalled = true;
            IsRecording = true;
        }

        public Stream Stop()
        {
            StopCalled = true;
            IsRecording = false;
            return new MemoryStream(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // fake "RIFF"
        }

        public List<AudioDevice> EnumerateDevices() => new();

        public void TriggerFault(Exception ex) => RecordingFaulted?.Invoke(ex);

        public void Reset()
        {
            StartCalled = false;
            StopCalled = false;
            LastDeviceName = null;
            StartException = null;
            IsRecording = false;
        }

        public void Dispose() { }
    }

    private sealed class FakeSttProvider : ISTTProvider
    {
        public string Result { get; set; } = "transcribed text";
        public Exception? ExceptionToThrow { get; set; }
        public bool WasCalled { get; private set; }
        public Stream? LastAudio { get; private set; }

        public Task<string> TranscribeAsync(Stream audio, CancellationToken ct)
        {
            WasCalled = true;
            LastAudio = audio;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; set; }
        public string? GetText() => Text;
        public void SetText(string text) => Text = text;
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public bool SuccessPlayed { get; private set; }
        public bool ErrorPlayed { get; private set; }
        public bool RecordingStartPlayed { get; private set; }
        public bool RecordingStopPlayed { get; private set; }
        public void PlaySuccess() => SuccessPlayed = true;
        public void PlayError() => ErrorPlayed = true;
        public void PlayRecordingStart() => RecordingStartPlayed = true;
        public void PlayRecordingStop() => RecordingStopPlayed = true;

        public void Reset()
        {
            SuccessPlayed = false;
            ErrorPlayed = false;
            RecordingStartPlayed = false;
            RecordingStopPlayed = false;
        }
    }

    private sealed class FakeTrayNotifier : ITrayNotifier
    {
        public string? LastInfoMessage { get; private set; }
        public string? LastErrorMessage { get; private set; }
        public void ShowNotification(string title, string message) => LastInfoMessage = message;
        public void ShowError(string title, string message) => LastErrorMessage = message;
    }
}
