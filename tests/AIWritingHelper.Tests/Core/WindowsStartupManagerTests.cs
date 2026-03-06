using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using Xunit;

namespace AIWritingHelper.Tests.Core;

[Trait("Category", "Integration")]
public class WindowsStartupManagerTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AIWritingHelper";

    public void Dispose()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    [Fact]
    public void SyncStartupState_WhenEnabled_CreatesRegistryEntry()
    {
        var settings = new AppSettings { StartWithWindows = true };
        var manager = new WindowsStartupManager(settings, NullLogger<WindowsStartupManager>.Instance);

        var result = manager.SyncStartupState();

        Assert.True(result);
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var value = key?.GetValue(ValueName) as string;
        Assert.NotNull(value);
        Assert.Contains(Environment.ProcessPath!, value);
    }

    [Fact]
    public void SyncStartupState_WhenDisabled_RemovesRegistryEntry()
    {
        // Pre-create entry
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            key!.SetValue(ValueName, "\"dummy.exe\"");

        var settings = new AppSettings { StartWithWindows = false };
        var manager = new WindowsStartupManager(settings, NullLogger<WindowsStartupManager>.Instance);

        var result = manager.SyncStartupState();

        Assert.True(result);
        using var readKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        Assert.Null(readKey?.GetValue(ValueName));
    }

    [Fact]
    public void SyncStartupState_WhenDisabledAndNoEntry_Succeeds()
    {
        var settings = new AppSettings { StartWithWindows = false };
        var manager = new WindowsStartupManager(settings, NullLogger<WindowsStartupManager>.Instance);

        var result = manager.SyncStartupState();

        Assert.True(result);
    }

    [Fact]
    public void SyncStartupState_WhenEnabledAndAlreadyPresent_UpdatesPath()
    {
        // Pre-create entry with old path
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            key!.SetValue(ValueName, "\"C:\\old\\path.exe\"");

        var settings = new AppSettings { StartWithWindows = true };
        var manager = new WindowsStartupManager(settings, NullLogger<WindowsStartupManager>.Instance);

        var result = manager.SyncStartupState();

        Assert.True(result);
        using var readKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var value = readKey?.GetValue(ValueName) as string;
        Assert.NotNull(value);
        Assert.Contains(Environment.ProcessPath!, value);
        Assert.DoesNotContain("old", value);
    }
}
