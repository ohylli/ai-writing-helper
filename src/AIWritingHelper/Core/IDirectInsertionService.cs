namespace AIWritingHelper.Core;

public interface IDirectInsertionService
{
    Task InsertAsync(string text, CancellationToken ct);
}
