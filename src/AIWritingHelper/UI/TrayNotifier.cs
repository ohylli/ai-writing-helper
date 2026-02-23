using AIWritingHelper.Core;

namespace AIWritingHelper.UI;

internal sealed class TrayNotifier : ITrayNotifier
{
    private readonly TrayApplicationContext _trayContext;

    public TrayNotifier(TrayApplicationContext trayContext)
    {
        _trayContext = trayContext;
    }

    public void ShowNotification(string title, string message)
    {
        _trayContext.NotifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
    }
}
