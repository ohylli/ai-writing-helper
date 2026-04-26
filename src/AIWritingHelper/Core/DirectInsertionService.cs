using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Core;

public sealed class DirectInsertionService
{
    private const int PasteRestoreDelayMs = 150;

    private readonly IClipboardService _clipboard;
    private readonly IInputSimulator _input;
    private readonly ILogger<DirectInsertionService> _logger;

    public DirectInsertionService(
        IClipboardService clipboard,
        IInputSimulator input,
        ILogger<DirectInsertionService> logger)
    {
        _clipboard = clipboard;
        _input = input;
        _logger = logger;
    }

    public async Task InsertAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Save the original clipboard text so we can restore it after the paste.
        // Non-text formats (images, files) are not preserved — see design.md.
        var original = _clipboard.GetText();

        try
        {
            _clipboard.SetText(text);
            _input.SendPaste();
            // Give the focused app time to process the synthetic Ctrl+V before we
            // overwrite the clipboard with the restored value. Inherently fragile —
            // see design.md "Direct Text Insertion" notes.
            await Task.Delay(PasteRestoreDelayMs, ct);
        }
        finally
        {
            // Clipboard.SetText("") throws ArgumentException, and there's nothing
            // useful to restore in that case anyway.
            if (!string.IsNullOrEmpty(original))
            {
                try
                {
                    _clipboard.SetText(original);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore clipboard after direct insertion");
                }
            }
        }
    }
}
