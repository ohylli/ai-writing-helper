namespace AIWritingHelper.Core;

public interface IClipboardService
{
    string? GetText();
    void SetText(string text);
}
