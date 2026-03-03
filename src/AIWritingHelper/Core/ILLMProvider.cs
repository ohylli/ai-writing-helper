namespace AIWritingHelper.Core;

public interface ILLMProvider
{
    Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct);
    Task<string> FixTextAsync(string text, string systemPrompt, string apiEndpoint, string apiKey, string modelName, CancellationToken ct);
}
