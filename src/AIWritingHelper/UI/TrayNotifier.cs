using AIWritingHelper.Core;

namespace AIWritingHelper.UI;

internal sealed class TrayNotifier : ITrayNotifier
{
    private readonly TrayApplicationContext _trayContext;

    public TrayNotifier(TrayApplicationContext trayContext)
    {
        _trayContext = trayContext;
    }

    public void ShowNotification(string title, string message) =>
        ShowBalloonTip(title, message, ToolTipIcon.Info);

    public void ShowError(string title, string message) =>
        ShowBalloonTip(title, message, ToolTipIcon.Error);

    private void ShowBalloonTip(string title, string message, ToolTipIcon icon) =>
        _trayContext.NotifyIcon.ShowBalloonTip(5000, title, message, icon);
}
