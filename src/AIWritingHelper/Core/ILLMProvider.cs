namespace AIWritingHelper.Core;

public interface ILLMProvider
{
    Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct);
}
