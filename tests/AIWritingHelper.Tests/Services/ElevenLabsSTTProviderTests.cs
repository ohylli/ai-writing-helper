using System.Net;
using System.Text;
using AIWritingHelper.Config;
using AIWritingHelper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Services;

public class ElevenLabsSTTProviderTests
{
    private const string ValidResponse = """
        {
            "text": "hello world",
            "language_code": "eng",
            "language_probability": 0.99,
            "words": []
        }
        """;

    private static AppSettings DefaultSettings() => new()
    {
        SttApiKey = "test-api-key",
        SttModelName = "scribe_v2",
    };

    private static ElevenLabsSTTProvider CreateProvider(
        AppSettings settings,
        FakeHttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var factory = new FakeHttpClientFactory(handler);
        var logger = NullLoggerFactory.Instance.CreateLogger<ElevenLabsSTTProvider>();
        return new ElevenLabsSTTProvider(settings, factory, logger, timeout ?? TimeSpan.FromSeconds(5));
    }

    private static MemoryStream MakeWav() => new([0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00], writable: false);

    [Fact]
    public async Task TranscribeAsync_Success_ReturnsText()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        var result = await provider.TranscribeAsync(MakeWav(), CancellationToken.None);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task TranscribeAsync_SendsCorrectRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        await provider.TranscribeAsync(MakeWav(), CancellationToken.None);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.elevenlabs.io/v1/speech-to-text", request.RequestUri!.ToString());
        Assert.True(request.Headers.TryGetValues("xi-api-key", out var keyValues));
        Assert.Equal("test-api-key", keyValues!.Single());

        var contentType = request.Content!.Headers.ContentType!;
        Assert.Equal("multipart/form-data", contentType.MediaType);

        // Multipart body field names, values, and headers are ASCII, so we can peek at the
        // raw bytes as UTF-8 to verify structural parts. Names may or may not be quoted
        // depending on the framework version, so match the field name + value separately.
        var bodyText = Encoding.UTF8.GetString(handler.LastRequestBodyBytes!);
        Assert.Contains("model_id", bodyText);
        Assert.Contains("scribe_v2", bodyText);
        Assert.Contains("audio.wav", bodyText);
        Assert.Contains("audio/wav", bodyText);
    }

    [Fact]
    public async Task TranscribeAsync_HttpError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, """{"detail":"invalid key"}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_HttpError_DoesNotLeakResponseBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, """{"detail":"secret info"}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain("secret info", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_Timeout_ThrowsTimeoutException()
    {
        var handler = new FakeHttpMessageHandler(delay: TimeSpan.FromSeconds(10));
        var provider = CreateProvider(DefaultSettings(), handler, timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler(delay: TimeSpan.FromSeconds(10));
        var provider = CreateProvider(DefaultSettings(), handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.TranscribeAsync(MakeWav(), cts.Token));
    }

    [Fact]
    public async Task TranscribeAsync_MalformedJson_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "not json at all {{{");
        var provider = CreateProvider(DefaultSettings(), handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_EmptyText_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":""}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("empty text", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_NullText_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":null}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("empty text", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_NullAudio_ThrowsArgumentNullException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.TranscribeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.SttApiKey = "";
        var provider = CreateProvider(settings, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_MissingModelName_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.SttModelName = "";
        var provider = CreateProvider(settings, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(MakeWav(), CancellationToken.None));

        Assert.Contains("model name", ex.Message);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly TimeSpan? _delay;

        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[]? LastRequestBodyBytes { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string responseBody = "", TimeSpan? delay = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            if (_delay.HasValue)
            {
                await Task.Delay(_delay.Value, cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
