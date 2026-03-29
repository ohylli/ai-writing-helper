namespace AIWritingHelper.Core;

public interface IAudioRecorder : IDisposable
{
    bool IsRecording { get; }
    event Action<Exception>? RecordingFaulted;
    void Start(string? deviceName);
    Stream Stop();
    List<AudioDevice> EnumerateDevices();
}
