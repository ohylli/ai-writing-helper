using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.UI;

internal sealed class TrayApplicationContext : ApplicationContext, ITrayNotifier
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly GlobalHotkeyManager _hotkeyManager;
    // Lazy to break circular DI: this class → TypoFixService → ITrayNotifier → this class.
    private readonly Lazy<TypoFixService> _typoFixService;
    private readonly CancellationTokenSource _appCts;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        GlobalHotkeyManager hotkeyManager,
        Lazy<TypoFixService> typoFixService,
        AppSettings appSettings,
        CancellationTokenSource appCts)
    {
        _logger = logger;
        _hotkeyManager = hotkeyManager;
        _typoFixService = typoFixService;
        _appCts = appCts;

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) =>
        {
            _logger.LogInformation("User requested quit");
            Application.Exit();
        };

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI Writing Helper",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeys(appSettings);

        _logger.LogInformation("Tray icon created");
    }

    private void RegisterHotkeys(AppSettings settings)
    {
        RegisterSingleHotkey(GlobalHotkeyManager.TypoFixHotkeyId, settings.TypoFixHotkey, "Typo Fix");
        RegisterSingleHotkey(GlobalHotkeyManager.DictationHotkeyId, settings.DictationHotkey, "Dictation");
    }

    private void RegisterSingleHotkey(int id, string hotkeyString, string label)
    {
        try
        {
            if (!_hotkeyManager.Register(id, hotkeyString))
            {
                ShowError("Hotkey Conflict", $"Could not register {label} hotkey ({hotkeyString}). It may be in use by another application.");
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid hotkey string for {Label}: {Hotkey}", label, hotkeyString);
            ShowError("Invalid Hotkey", $"The {label} hotkey '{hotkeyString}' is not valid.");
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        switch (e.HotkeyId)
        {
            case GlobalHotkeyManager.TypoFixHotkeyId:
                _logger.LogDebug("Typo fix hotkey pressed");
                _ = _typoFixService.Value.ExecuteAsync(_appCts.Token);
                break;

            case GlobalHotkeyManager.DictationHotkeyId:
                _logger.LogDebug("Dictation hotkey pressed (not yet implemented)");
                break;
        }
    }

    public void ShowNotification(string title, string message) =>
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);

    public void ShowError(string title, string message) =>
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyManager.UnregisterAll();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
