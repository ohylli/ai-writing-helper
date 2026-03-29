using Microsoft.Extensions.Logging;
using NAudio.Wave;
using AIWritingHelper.Core;

namespace AIWritingHelper.Audio;

internal sealed class MicrophoneRecorder : IAudioRecorder
{
    private readonly ILogger<MicrophoneRecorder> _logger;
    private readonly TimeSpan _maxDuration;
    private readonly object _lock = new();

    private WaveInEvent? _waveIn;
    private MemoryStream? _memoryStream;
    private WaveFileWriter? _waveFileWriter;
    private System.Threading.Timer? _maxDurationTimer;
    private bool _autoStopped;
    private bool _disposed;

    public event Action<Exception>? RecordingFaulted;

    public MicrophoneRecorder(ILogger<MicrophoneRecorder> logger)
        : this(logger, TimeSpan.FromHours(1))
    {
    }

    internal MicrophoneRecorder(ILogger<MicrophoneRecorder> logger, TimeSpan maxDuration)
    {
        _logger = logger;
        _maxDuration = maxDuration;
    }

    public bool IsRecording
    {
        get { lock (_lock) { return _waveIn != null; } }
    }

    public void Start(string? deviceName)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waveIn != null)
                throw new InvalidOperationException("Recording is already in progress.");

            int deviceNumber = ResolveDevice(deviceName);

            _autoStopped = false;
            _memoryStream = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1)
            };
            _waveFileWriter = new WaveFileWriter(_memoryStream, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            _maxDurationTimer = new System.Threading.Timer(
                _ => AutoStop(),
                null,
                _maxDuration,
                Timeout.InfiniteTimeSpan);

            _logger.LogInformation("Recording started on device {DeviceNumber}", deviceNumber);
        }
    }

    public Stream Stop()
    {
        WaveInEvent waveIn;
        WaveFileWriter writer;
        MemoryStream ms;
        System.Threading.Timer? timer;
        bool alreadyStopped;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waveIn == null)
                throw new InvalidOperationException("No recording is in progress.");

            // Snapshot and clear fields while holding the lock.
            // Nulling _waveFileWriter ensures OnDataAvailable (which also takes _lock)
            // will no-op if a late callback arrives after we release.
            waveIn = _waveIn;
            writer = _waveFileWriter!;
            ms = _memoryStream!;
            timer = _maxDurationTimer;
            alreadyStopped = _autoStopped;

            _waveIn = null;
            _waveFileWriter = null;
            _memoryStream = null;
            _maxDurationTimer = null;
            _autoStopped = false;
        }

        // StopRecording fires a final DataAvailable callback synchronously,
        // so it must be called outside the lock to avoid deadlock.
        timer?.Dispose();
        waveIn.DataAvailable -= OnDataAvailable;
        waveIn.RecordingStopped -= OnRecordingStopped;
        if (!alreadyStopped)
            waveIn.StopRecording();
        waveIn.Dispose();

        // Dispose flushes the WAV/RIFF header into ms — must happen before ToArray.
        writer.Dispose();

        var audioData = ms.ToArray();
        ms.Dispose();

        _logger.LogInformation("Recording stopped, {Bytes} bytes captured", audioData.Length);
        return new MemoryStream(audioData, writable: false);
    }

    public List<AudioDevice> EnumerateDevices()
    {
        var devices = new List<AudioDevice>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(new AudioDevice(i, caps.ProductName));
        }
        return devices;
    }

    public void Dispose()
    {
        WaveInEvent? waveIn;
        WaveFileWriter? writer;
        MemoryStream? ms;
        System.Threading.Timer? timer;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            waveIn = _waveIn;
            writer = _waveFileWriter;
            ms = _memoryStream;
            timer = _maxDurationTimer;

            _waveIn = null;
            _waveFileWriter = null;
            _memoryStream = null;
            _maxDurationTimer = null;
        }

        // Dispose outside the lock to avoid deadlock with OnDataAvailable.
        timer?.Dispose();
        if (waveIn != null)
        {
            waveIn.DataAvailable -= OnDataAvailable;
            waveIn.RecordingStopped -= OnRecordingStopped;
            try { waveIn.StopRecording(); } catch { }
            waveIn.Dispose();
        }
        writer?.Dispose();
        ms?.Dispose();
    }

    private int ResolveDevice(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return 0;

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            if (caps.ProductName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        _logger.LogWarning("Microphone '{DeviceName}' not found, falling back to default", deviceName);
        return 0;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _waveFileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped due to error");
            RecordingFaulted?.Invoke(e.Exception);
        }
    }

    private void AutoStop()
    {
        WaveInEvent? waveIn;

        lock (_lock)
        {
            if (_waveIn == null || _disposed) return;
            _logger.LogWarning("Recording auto-stopped after maximum duration");
            _autoStopped = true;

            waveIn = _waveIn;
            waveIn.DataAvailable -= OnDataAvailable;
            waveIn.RecordingStopped -= OnRecordingStopped;
        }

        // StopRecording outside the lock to avoid deadlock.
        try { waveIn.StopRecording(); } catch { }
    }
}
