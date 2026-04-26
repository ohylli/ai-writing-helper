using AIWritingHelper.Config;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Core;

public class DictationService
{
    private readonly IAudioRecorder _recorder;
    private readonly ISTTProvider _stt;
    private readonly IClipboardService _clipboard;
    private readonly IDirectInsertionService _directInsertion;
    private readonly ISoundPlayer _sound;
    private readonly ITrayNotifier _notifier;
    private readonly AppSettings _settings;
    private readonly OperationLock _lock;
    private readonly ILogger<DictationService> _logger;

    // Toggle state. Read/written from the WinForms UI thread (hotkey dispatch),
    // and additionally cleared from the NAudio fault callback. OperationLock is
    // the hard backstop against any residual races between presses.
    private bool _isRecording;

    public DictationService(
        IAudioRecorder recorder,
        ISTTProvider stt,
        IClipboardService clipboard,
        IDirectInsertionService directInsertion,
        ISoundPlayer sound,
        ITrayNotifier notifier,
        AppSettings settings,
        OperationLock operationLock,
        ILogger<DictationService> logger)
    {
        _recorder = recorder;
        _stt = stt;
        _clipboard = clipboard;
        _directInsertion = directInsertion;
        _sound = sound;
        _notifier = notifier;
        _settings = settings;
        _lock = operationLock;
        _logger = logger;
    }

    public async Task ToggleAsync(CancellationToken ct)
    {
        if (_isRecording)
        {
            await StopAndTranscribeAsync(ct);
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (!_lock.TryAcquire())
        {
            _logger.LogWarning("Dictation rejected — another operation is in progress");
            _sound.PlayError();
            return;
        }

        _recorder.RecordingFaulted += OnRecordingFaulted;

        try
        {
            var deviceName = string.IsNullOrEmpty(_settings.MicrophoneDeviceName)
                ? null
                : _settings.MicrophoneDeviceName;

            _recorder.Start(deviceName);
            _isRecording = true;
            _sound.PlayRecordingStart();
            _logger.LogInformation("Dictation recording started");
        }
        catch (Exception ex)
        {
            _recorder.RecordingFaulted -= OnRecordingFaulted;
            _lock.Release();
            _sound.PlayError();
            _notifier.ShowError("Dictation Error", "Could not start microphone");
            _logger.LogWarning(ex, "Failed to start recording");
        }
    }

    private async Task StopAndTranscribeAsync(CancellationToken ct)
    {
        // Clear the flag and unsubscribe before any awaits so a third hotkey
        // press during transcription correctly hits the busy path (lock still held).
        _isRecording = false;
        _recorder.RecordingFaulted -= OnRecordingFaulted;

        Stream? audio = null;
        try
        {
            audio = _recorder.Stop();
            _sound.PlayRecordingStop();

            audio.Position = 0;
            _logger.LogInformation("Dictation transcribing ({Bytes} bytes)", audio.Length);
            var text = await _stt.TranscribeAsync(audio, ct);

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogInformation("Dictation produced no text — silence or no speech detected");
                _sound.PlayError();
                _notifier.ShowNotification("Dictation", "No speech detected");
                return;
            }

            if (_settings.DictationOutputMode == DictationOutputMode.DirectInsertion)
            {
                await _directInsertion.InsertAsync(text, ct);
            }
            else
            {
                _clipboard.SetText(text);
            }
            _sound.PlaySuccess();
            _logger.LogInformation("Dictation completed, {Length} chars", text.Length);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dictation cancelled");
            _sound.PlayError();
        }
        catch (Exception ex)
        {
            _sound.PlayError();

            var message = ex switch
            {
                TimeoutException => "Request timed out",
                HttpRequestException => "Could not reach the speech service",
                InvalidOperationException => ex.Message,
                _ => "Unexpected error",
            };

            _notifier.ShowError("Dictation Error", message);
            _logger.LogWarning(ex, "Dictation failed: {Message}", message);
        }
        finally
        {
            audio?.Dispose();
            _lock.Release();
        }
    }

    private void OnRecordingFaulted(Exception ex)
    {
        if (!_isRecording)
        {
            return;
        }

        _logger.LogError(ex, "Microphone faulted during recording");
        _isRecording = false;
        _recorder.RecordingFaulted -= OnRecordingFaulted;

        try
        {
            using var _ = _recorder.Stop();
        }
        catch (Exception stopEx)
        {
            _logger.LogDebug(stopEx, "Recorder.Stop after fault threw — ignoring");
        }

        _lock.Release();
        _sound.PlayError();
        _notifier.ShowError("Dictation Error", "Microphone error during recording");
    }
}
