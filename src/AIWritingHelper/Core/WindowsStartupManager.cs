using AIWritingHelper.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIWritingHelper.Core;

internal sealed class WindowsStartupManager : IStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AIWritingHelper";

    private readonly AppSettings _settings;
    private readonly ILogger<WindowsStartupManager> _logger;

    public WindowsStartupManager(AppSettings settings, ILogger<WindowsStartupManager> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool SyncStartupState()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogError("Could not open registry key {KeyPath}", RunKeyPath);
                return false;
            }

            if (_settings.StartWithWindows)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    _logger.LogError("Could not determine executable path");
                    return false;
                }

                key.SetValue(ValueName, $"\"{exePath}\"");
                _logger.LogInformation("Startup registry entry set to {ExePath}", exePath);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.LogInformation("Startup registry entry removed");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync startup registry state");
            return false;
        }
    }
}
