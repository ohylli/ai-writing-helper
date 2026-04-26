using NAudio.Wave;

namespace AIWritingHelper.Audio;

internal static class SilentWavGenerator
{
    public static Stream CreateSilentWav(TimeSpan duration)
    {
        const int sampleRate = 16000;
        const int bitsPerSample = 16;
        const int channels = 1;

        var format = new WaveFormat(sampleRate, bitsPerSample, channels);
        var sampleCount = (int)(duration.TotalSeconds * sampleRate);
        var silentBytes = new byte[sampleCount * channels * (bitsPerSample / 8)];

        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(ms, format))
        {
            writer.Write(silentBytes, 0, silentBytes.Length);
        }

        return new MemoryStream(ms.ToArray(), writable: false);
    }
}
