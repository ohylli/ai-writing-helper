using System.Runtime.ExceptionServices;

namespace AIWritingHelper.Core;

internal sealed class ClipboardService : IClipboardService
{
    public string? GetText()
    {
        return RunOnSta(() =>
        {
            if (Clipboard.ContainsText())
                return Clipboard.GetText();
            return null;
        });
    }

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        RunOnSta(() => Clipboard.SetText(text));
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return action();

        T result = default!;
        ExceptionDispatchInfo? caught = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                caught = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        caught?.Throw();

        return result;
    }

    private static void RunOnSta(Action action)
    {
        RunOnSta<object?>(() => { action(); return null; });
    }
}
