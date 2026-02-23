namespace AIWritingHelper.Core;

public interface ISTTProvider
{
    Task<string> TranscribeAsync(Stream audio, CancellationToken ct);
}
