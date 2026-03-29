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
    private bool _disposed;

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
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waveIn == null)
                throw new InvalidOperationException("No recording is in progress.");

            return StopAndReturnStream();
        }
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
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _maxDurationTimer?.Dispose();
            if (_waveIn != null)
            {
                try { _waveIn.StopRecording(); } catch { }
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
            }
            _waveFileWriter?.Dispose();
            _memoryStream?.Dispose();
        }
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
            _logger.LogError(e.Exception, "Recording stopped due to error");
    }

    private void AutoStop()
    {
        lock (_lock)
        {
            if (_waveIn == null || _disposed) return;
            _logger.LogWarning("Recording auto-stopped after maximum duration");
            try { _waveIn.StopRecording(); } catch { }
        }
    }

    private Stream StopAndReturnStream()
    {
        _maxDurationTimer?.Dispose();
        _maxDurationTimer = null;

        _waveIn!.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;

        _waveFileWriter!.Dispose();
        _waveFileWriter = null;

        var audioData = _memoryStream!.ToArray();
        _memoryStream = null;

        _logger.LogInformation("Recording stopped, {Bytes} bytes captured", audioData.Length);
        return new MemoryStream(audioData, writable: false);
    }
}
