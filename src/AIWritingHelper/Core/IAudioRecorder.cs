namespace AIWritingHelper.Core;

public interface IAudioRecorder : IDisposable
{
    bool IsRecording { get; }
    void Start(string? deviceName);
    Stream Stop();
    List<AudioDevice> EnumerateDevices();
}
