using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Serilog.Core;

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
    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ILLMProvider _llmProvider;
    private readonly ILoggerFactory _loggerFactory;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        GlobalHotkeyManager hotkeyManager,
        Lazy<TypoFixService> typoFixService,
        AppSettings appSettings,
        CancellationTokenSource appCts,
        SettingsManager settingsManager,
        LoggingLevelSwitch levelSwitch,
        ILLMProvider llmProvider,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _hotkeyManager = hotkeyManager;
        _typoFixService = typoFixService;
        _appCts = appCts;
        _settings = appSettings;
        _settingsManager = settingsManager;
        _levelSwitch = levelSwitch;
        _llmProvider = llmProvider;
        _loggerFactory = loggerFactory;

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += OnSettingsClick;

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) =>
        {
            _logger.LogInformation("User requested quit");
            Application.Exit();
        };

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI Writing Helper",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeys(_settings);

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
                var task = _typoFixService.Value.ExecuteAsync(_appCts.Token);
                _ = task.ContinueWith(
                    t => _logger.LogError(t.Exception, "Unhandled exception in typo fix"),
                    TaskContinuationOptions.OnlyOnFaulted);
                break;

            case GlobalHotkeyManager.DictationHotkeyId:
                _logger.LogDebug("Dictation hotkey pressed (not yet implemented)");
                break;
        }
    }

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(
            _settings,
            _settingsManager,
            _levelSwitch,
            _llmProvider,
            _hotkeyManager,
            _loggerFactory.CreateLogger<SettingsForm>());

        _settingsForm.ShowDialog();
        _settingsForm.Dispose();
        _settingsForm = null;
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
