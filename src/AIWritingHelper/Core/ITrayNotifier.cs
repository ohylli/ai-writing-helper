namespace AIWritingHelper.Core;

public interface ITrayNotifier
{
    void ShowNotification(string title, string message);
    void ShowError(string title, string message);
}
