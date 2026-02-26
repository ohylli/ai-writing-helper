using AIWritingHelper.Config;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Core;

public class TypoFixService
{
    private readonly IClipboardService _clipboard;
    private readonly ILLMProvider _llm;
    private readonly ISoundPlayer _sound;
    private readonly ITrayNotifier _notifier;
    private readonly AppSettings _settings;
    private readonly OperationLock _lock;
    private readonly ILogger<TypoFixService> _logger;

    public TypoFixService(
        IClipboardService clipboard,
        ILLMProvider llm,
        ISoundPlayer sound,
        ITrayNotifier notifier,
        AppSettings settings,
        OperationLock operationLock,
        ILogger<TypoFixService> logger)
    {
        _clipboard = clipboard;
        _llm = llm;
        _sound = sound;
        _notifier = notifier;
        _settings = settings;
        _lock = operationLock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_lock.TryAcquire())
        {
            _logger.LogWarning("Typo fix rejected — another operation is in progress");
            _sound.PlayError();
            return;
        }

        try
        {
            var text = _clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                _logger.LogInformation("Typo fix skipped — clipboard is empty");
                _sound.PlayError();
                _notifier.ShowError("Typo Fix", "No text on clipboard");
                return;
            }

            _logger.LogInformation("Typo fix starting ({Length} chars)", text.Length);
            var result = await _llm.FixTextAsync(text, _settings.LlmSystemPrompt, ct);
            _clipboard.SetText(result);
            _sound.PlaySuccess();
            _logger.LogInformation("Typo fix completed");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Typo fix cancelled");
            _sound.PlayError();
        }
        catch (Exception ex)
        {
            _sound.PlayError();

            var message = ex switch
            {
                TimeoutException => "Request timed out",
                HttpRequestException => "Could not reach the LLM service",
                // Currently only LLM provider throws these with user-friendly messages.
                // If other sources start throwing InvalidOperationException, consider
                // introducing a dedicated exception type to avoid leaking internal details.
                InvalidOperationException => ex.Message,
                _ => "Unexpected error",
            };

            _notifier.ShowError("Typo Fix Error", message);
            _logger.LogWarning(ex, "Typo fix failed: {Message}", message);
        }
        finally
        {
            _lock.Release();
        }
    }
}
