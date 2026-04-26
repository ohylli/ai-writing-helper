namespace AIWritingHelper.Core;

public interface ISTTProvider
{
    Task<string> TranscribeAsync(Stream audio, CancellationToken ct);
    Task<string> TranscribeAsync(Stream audio, string apiKey, string modelName, CancellationToken ct);
}
