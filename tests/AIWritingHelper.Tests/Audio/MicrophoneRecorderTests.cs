using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Xunit;
using AIWritingHelper.Audio;

namespace AIWritingHelper.Tests.Audio;

public class MicrophoneRecorderTests
{
    private static MicrophoneRecorder CreateRecorder(TimeSpan? maxDuration = null)
    {
        var logger = NullLogger<MicrophoneRecorder>.Instance;
        return maxDuration.HasValue
            ? new MicrophoneRecorder(logger, maxDuration.Value)
            : new MicrophoneRecorder(logger);
    }

    [Fact]
    public void IsRecording_InitiallyFalse()
    {
        using var recorder = CreateRecorder();
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void Stop_WhenNotRecording_Throws()
    {
        using var recorder = CreateRecorder();
        Assert.Throws<InvalidOperationException>(() => recorder.Stop());
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var recorder = CreateRecorder();
        recorder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => recorder.Start(null));
    }

    [Fact]
    public void Stop_AfterDispose_Throws()
    {
        var recorder = CreateRecorder();
        recorder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => recorder.Stop());
    }

    [Fact]
    public void EnumerateDevices_DoesNotThrow()
    {
        using var recorder = CreateRecorder();
        var devices = recorder.EnumerateDevices();
        Assert.NotNull(devices);
    }

    [SkippableFact]
    public async Task StartAndStop_ReturnsValidWavStream()
    {
        Skip.If(WaveInEvent.DeviceCount == 0, "No audio input devices available");

        using var recorder = CreateRecorder();
        recorder.Start(null);
        Assert.True(recorder.IsRecording);

        await Task.Delay(300);

        using var stream = recorder.Stop();
        Assert.False(recorder.IsRecording);
        Assert.True(stream.Length > 0);

        // Verify RIFF header
        var header = new byte[4];
        stream.ReadExactly(header);
        Assert.Equal("RIFF"u8.ToArray(), header);
    }

    [SkippableFact]
    public void Start_WhenAlreadyRecording_Throws()
    {
        Skip.If(WaveInEvent.DeviceCount == 0, "No audio input devices available");

        using var recorder = CreateRecorder();
        recorder.Start(null);
        try
        {
            Assert.Throws<InvalidOperationException>(() => recorder.Start(null));
        }
        finally
        {
            recorder.Stop().Dispose();
        }
    }

    [SkippableFact]
    public void Dispose_DuringRecording_DoesNotThrow()
    {
        Skip.If(WaveInEvent.DeviceCount == 0, "No audio input devices available");

        var recorder = CreateRecorder();
        recorder.Start(null);
        recorder.Dispose();
    }

    [SkippableFact]
    public async Task AutoStop_AfterMaxDuration_StillReturnsStream()
    {
        Skip.If(WaveInEvent.DeviceCount == 0, "No audio input devices available");

        using var recorder = CreateRecorder(TimeSpan.FromSeconds(2));
        recorder.Start(null);

        await Task.Delay(3000);

        // IsRecording is still true (resources allocated, waiting for Stop())
        Assert.True(recorder.IsRecording);

        using var stream = recorder.Stop();
        Assert.False(recorder.IsRecording);
        Assert.True(stream.Length > 0);

        // Verify RIFF header
        var header = new byte[4];
        stream.ReadExactly(header);
        Assert.Equal("RIFF"u8.ToArray(), header);
    }
}
