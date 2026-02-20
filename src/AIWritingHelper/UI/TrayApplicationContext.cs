using Microsoft.Extensions.Logging;

namespace AIWritingHelper.UI;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
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

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI Writing Helper",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _logger.LogInformation("Tray icon created");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
