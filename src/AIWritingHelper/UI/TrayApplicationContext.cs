using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.UI;

internal sealed class TrayApplicationContext : ApplicationContext, ITrayNotifier
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ILogger<TrayApplicationContext> _logger;

    public TrayApplicationContext(ILogger<TrayApplicationContext> logger)
    {
        _logger = logger;

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

        _logger.LogInformation("Tray icon created");
    }

    public void ShowNotification(string title, string message) =>
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);

    public void ShowError(string title, string message) =>
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
