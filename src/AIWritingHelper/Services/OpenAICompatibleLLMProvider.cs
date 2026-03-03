using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Services;

internal sealed class OpenAICompatibleLLMProvider : ILLMProvider
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAICompatibleLLMProvider> _logger;
    private readonly TimeSpan _timeout;

    public OpenAICompatibleLLMProvider(
        AppSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAICompatibleLLMProvider> logger)
        : this(settings, httpClientFactory, logger, DefaultTimeout)
    {
    }

    internal OpenAICompatibleLLMProvider(
        AppSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAICompatibleLLMProvider> logger,
        TimeSpan timeout)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _timeout = timeout;
    }

    public Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct)
        => FixTextCoreAsync(text, systemPrompt, _settings.LlmApiEndpoint, _settings.LlmApiKey, _settings.LlmModelName, ct);

    public Task<string> FixTextAsync(string text, string systemPrompt, string apiEndpoint, string apiKey, string modelName, CancellationToken ct)
        => FixTextCoreAsync(text, systemPrompt, apiEndpoint, apiKey, modelName, ct);

    private async Task<string> FixTextCoreAsync(
        string text, string systemPrompt,
        string apiEndpoint, string apiKey, string modelName,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        if (string.IsNullOrWhiteSpace(apiEndpoint))
            throw new InvalidOperationException("LLM API endpoint is not configured");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("LLM API key is not configured");
        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException("LLM model name is not configured");

        var url = apiEndpoint.TrimEnd('/') + "/chat/completions";

        var request = new ChatRequest
        {
            Model = modelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = text },
            ],
            Temperature = 0,
            Stream = false,
        };

        var json = JsonSerializer.Serialize(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        _logger.LogDebug("Sending LLM request to {Url} with model {Model}", url, modelName);

        var client = _httpClientFactory.CreateClient();
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"LLM request timed out after {_timeout.TotalSeconds}s");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogWarning("LLM API error {StatusCode}: {ErrorBody}", (int)response.StatusCode, body);
                throw new HttpRequestException(
                    $"LLM API returned {(int)response.StatusCode} {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token);

            ChatResponse? chatResponse;
            try
            {
                chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to parse LLM response JSON", ex);
            }

            if (chatResponse?.Choices is null || chatResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("LLM response contained no choices");
            }

            var content = chatResponse.Choices[0].Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException("LLM response contained empty content");
            }

            _logger.LogDebug("LLM returned {Length} characters", content.Length);
            return content;
        }
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatChoiceMessage? Message { get; set; }
    }

    private sealed class ChatChoiceMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
