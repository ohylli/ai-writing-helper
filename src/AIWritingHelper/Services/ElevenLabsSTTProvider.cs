using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Services;

internal sealed class ElevenLabsSTTProvider : ISTTProvider
{
    private const string ApiUrl = "https://api.elevenlabs.io/v1/speech-to-text";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ElevenLabsSTTProvider> _logger;
    private readonly TimeSpan _timeout;

    public ElevenLabsSTTProvider(
        AppSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<ElevenLabsSTTProvider> logger)
        : this(settings, httpClientFactory, logger, DefaultTimeout)
    {
    }

    internal ElevenLabsSTTProvider(
        AppSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<ElevenLabsSTTProvider> logger,
        TimeSpan timeout)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _timeout = timeout;
    }

    public async Task<string> TranscribeAsync(Stream audio, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var apiKey = _settings.SttApiKey;
        var modelName = _settings.SttModelName;

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("STT API key is not configured");
        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException("STT model name is not configured");

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(modelName), "model_id");

        // Wrap so MultipartFormDataContent.Dispose doesn't propagate to the caller's stream.
        var fileContent = new StreamContent(new NonDisposingStream(audio));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        multipart.Add(fileContent, "file", "audio.wav");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = multipart,
        };
        httpRequest.Headers.Add("xi-api-key", apiKey);

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        _logger.LogDebug("Sending STT request to {Url} with model {Model}", ApiUrl, modelName);

        var client = _httpClientFactory.CreateClient();
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"STT request timed out after {_timeout.TotalSeconds}s");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogWarning("STT API error {StatusCode}: {ErrorBody}", (int)response.StatusCode, body);
                throw new HttpRequestException(
                    $"ElevenLabs STT API returned {(int)response.StatusCode} {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token);

            TranscriptionResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<TranscriptionResponse>(responseJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to parse STT response JSON", ex);
            }

            var text = parsed?.Text;
            if (text is null)
            {
                throw new InvalidOperationException("STT response missing required 'text' field");
            }

            _logger.LogDebug("STT returned {Length} characters", text.Length);
            return text;
        }
    }

    private sealed class TranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class NonDisposingStream : Stream
    {
        private readonly Stream _inner;

        public NonDisposingStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing) { }
    }
}
