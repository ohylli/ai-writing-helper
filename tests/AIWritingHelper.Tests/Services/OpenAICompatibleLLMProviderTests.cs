using System.Net;
using System.Text;
using System.Text.Json;
using AIWritingHelper.Config;
using AIWritingHelper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Services;

public class OpenAICompatibleLLMProviderTests
{
    private const string ValidResponse = """
        {
            "choices": [
                {
                    "message": {
                        "content": "Fixed text here"
                    }
                }
            ]
        }
        """;

    private static AppSettings DefaultSettings() => new()
    {
        LlmApiEndpoint = "https://api.example.com/v1",
        LlmApiKey = "test-api-key",
        LlmModelName = "test-model",
    };

    private static OpenAICompatibleLLMProvider CreateProvider(
        AppSettings settings,
        FakeHttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var factory = new FakeHttpClientFactory(handler);
        var logger = NullLoggerFactory.Instance.CreateLogger<OpenAICompatibleLLMProvider>();
        return new OpenAICompatibleLLMProvider(settings, factory, logger, timeout ?? TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FixTextAsync_Success_ReturnsFixedText()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        var result = await provider.FixTextAsync("some text", "fix typos", CancellationToken.None);

        Assert.Equal("Fixed text here", result);
    }

    [Fact]
    public async Task FixTextAsync_SendsCorrectRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        var provider = CreateProvider(settings, handler);

        await provider.FixTextAsync("hello wrold", "Fix typos", CancellationToken.None);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("test-api-key", request.Headers.Authorization.Parameter);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(0.0, root.GetProperty("temperature").GetDouble());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Fix typos", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("hello wrold", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task FixTextAsync_TrailingSlashInBaseUrl_BuildsCorrectUrl()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.LlmApiEndpoint = "https://api.example.com/v1/";
        var provider = CreateProvider(settings, handler);

        await provider.FixTextAsync("text", "prompt", CancellationToken.None);

        Assert.Equal("https://api.example.com/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task FixTextAsync_HttpError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, """{"error":"invalid key"}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task FixTextAsync_Timeout_ThrowsTimeoutException()
    {
        var handler = new FakeHttpMessageHandler(delay: TimeSpan.FromSeconds(10));
        var provider = CreateProvider(DefaultSettings(), handler, timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));
    }

    [Fact]
    public async Task FixTextAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler(delay: TimeSpan.FromSeconds(10));
        var provider = CreateProvider(DefaultSettings(), handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.FixTextAsync("text", "prompt", cts.Token));
    }

    [Fact]
    public async Task FixTextAsync_MalformedJson_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "not json at all {{{");
        var provider = CreateProvider(DefaultSettings(), handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));
    }

    [Fact]
    public async Task FixTextAsync_EmptyChoices_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"choices":[]}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("no choices", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FixTextAsync_InvalidText_ThrowsArgumentException(string? text)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.FixTextAsync(text!, "prompt", CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FixTextAsync_InvalidSystemPrompt_ThrowsArgumentException(string? prompt)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var provider = CreateProvider(DefaultSettings(), handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.FixTextAsync("some text", prompt!, CancellationToken.None));
    }

    [Fact]
    public async Task FixTextAsync_MissingApiEndpoint_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.LlmApiEndpoint = "";
        var provider = CreateProvider(settings, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("endpoint", ex.Message);
    }

    [Fact]
    public async Task FixTextAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.LlmApiKey = "";
        var provider = CreateProvider(settings, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public async Task FixTextAsync_MissingModelName_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponse);
        var settings = DefaultSettings();
        settings.LlmModelName = "";
        var provider = CreateProvider(settings, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("model name", ex.Message);
    }

    [Fact]
    public async Task FixTextAsync_HttpError_DoesNotLeakResponseBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, """{"error":"secret info"}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain("secret info", ex.Message);
    }

    [Fact]
    public async Task FixTextAsync_NullContent_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":null}}]}""");
        var provider = CreateProvider(DefaultSettings(), handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.FixTextAsync("text", "prompt", CancellationToken.None));

        Assert.Contains("empty content", ex.Message);
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
        public string? LastRequestBody { get; private set; }

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
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
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
