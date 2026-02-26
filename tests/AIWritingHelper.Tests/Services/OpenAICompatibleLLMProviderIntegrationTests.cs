using AIWritingHelper.Config;
using AIWritingHelper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Services;

[Trait("Category", "Integration")]
public class OpenAICompatibleLLMProviderIntegrationTests
{
    private static string? LoadApiKey()
    {
        // Load .env from the output directory (copied there by the csproj if it exists).
        var assemblyDir = Path.GetDirectoryName(typeof(OpenAICompatibleLLMProviderIntegrationTests).Assembly.Location);
        if (assemblyDir is not null)
        {
            var envPath = Path.Combine(assemblyDir, ".env");
            if (File.Exists(envPath))
                DotNetEnv.Env.Load(envPath);
        }

        return Environment.GetEnvironmentVariable("LLM_API_KEY");
    }

    private static readonly string DefaultSystemPrompt = new AppSettings().LlmSystemPrompt;

    private static OpenAICompatibleLLMProvider CreateProvider(string apiKey)
    {
        var settings = new AppSettings { LlmApiKey = apiKey };
        var factory = new SimpleHttpClientFactory();
        var logger = NullLoggerFactory.Instance.CreateLogger<OpenAICompatibleLLMProvider>();
        return new OpenAICompatibleLLMProvider(settings, factory, logger);
    }

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [SkippableFact]
    public async Task FixTextAsync_RealApi_ReturnsFixedText()
    {
        var apiKey = LoadApiKey();
        Skip.If(string.IsNullOrEmpty(apiKey), "LLM_API_KEY not set — skipping integration test");

        var provider = CreateProvider(apiKey);

        var result = await provider.FixTextAsync(
            "hello wrold, how are yuo?",
            DefaultSystemPrompt,
            CancellationToken.None);

        Assert.Contains("world", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("you", result, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task FixTextAsync_RealApi_PreservesCorrectText()
    {
        var apiKey = LoadApiKey();
        Skip.If(string.IsNullOrEmpty(apiKey), "LLM_API_KEY not set — skipping integration test");

        var provider = CreateProvider(apiKey);
        var correctText = "The quick brown fox jumps over the lazy dog.";

        var result = await provider.FixTextAsync(
            correctText,
            DefaultSystemPrompt,
            CancellationToken.None);

        Assert.Equal(correctText, result);
    }

    [SkippableFact]
    public async Task FixTextAsync_InvalidApiKey_ThrowsHttpRequestException()
    {
        var apiKey = LoadApiKey();
        Skip.If(string.IsNullOrEmpty(apiKey), "LLM_API_KEY not set — skipping integration test");

        var provider = CreateProvider("invalid-key-that-should-not-work");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.FixTextAsync("hello world", DefaultSystemPrompt, CancellationToken.None));
    }
}
