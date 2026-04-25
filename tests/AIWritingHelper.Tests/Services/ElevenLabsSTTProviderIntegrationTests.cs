using AIWritingHelper.Config;
using AIWritingHelper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Utils;
using NAudio.Wave;
using Xunit;

namespace AIWritingHelper.Tests.Services;

[Trait("Category", "Integration")]
public class ElevenLabsSTTProviderIntegrationTests
{
    private static string? LoadApiKey()
    {
        // Load .env from the output directory (copied there by the csproj if it exists).
        var assemblyDir = Path.GetDirectoryName(typeof(ElevenLabsSTTProviderIntegrationTests).Assembly.Location);
        if (assemblyDir is not null)
        {
            var envPath = Path.Combine(assemblyDir, ".env");
            if (File.Exists(envPath))
                DotNetEnv.Env.Load(envPath);
        }

        return Environment.GetEnvironmentVariable("STT_API_KEY");
    }

    private static ElevenLabsSTTProvider CreateProvider(string apiKey)
    {
        var settings = new AppSettings { SttApiKey = apiKey };
        var factory = new SimpleHttpClientFactory();
        var logger = NullLoggerFactory.Instance.CreateLogger<ElevenLabsSTTProvider>();
        return new ElevenLabsSTTProvider(settings, factory, logger);
    }

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    // Produces the same WAV format MicrophoneRecorder emits in production: 16 kHz / 16-bit / mono.
    private static MemoryStream GenerateSilentWav(TimeSpan duration)
    {
        var outer = new MemoryStream();
        var format = new WaveFormat(16000, 16, 1);
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(outer), format))
        {
            int sampleCount = (int)(format.SampleRate * duration.TotalSeconds);
            var silence = new byte[sampleCount * 2];
            writer.Write(silence, 0, silence.Length);
        }
        outer.Position = 0;
        return outer;
    }

    [SkippableFact]
    public async Task TranscribeAsync_RealApi_AcceptsWavFormat()
    {
        var apiKey = LoadApiKey();
        Skip.If(string.IsNullOrEmpty(apiKey), "STT_API_KEY not set — skipping integration test");

        var provider = CreateProvider(apiKey);
        using var audio = GenerateSilentWav(TimeSpan.FromMilliseconds(500));

        // Validates the HTTP contract: the API accepts our WAV payload + auth header.
        // Silent audio yields an empty transcription, which is still proof the HTTP layer works.
        var result = await provider.TranscribeAsync(audio, CancellationToken.None);
        Assert.NotNull(result);
    }

    [SkippableFact]
    public async Task TranscribeAsync_InvalidApiKey_ThrowsHttpRequestException()
    {
        var apiKey = LoadApiKey();
        Skip.If(string.IsNullOrEmpty(apiKey), "STT_API_KEY not set — skipping integration test");

        var provider = CreateProvider("invalid-key-that-should-not-work");
        using var audio = GenerateSilentWav(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.TranscribeAsync(audio, CancellationToken.None));
    }
}
