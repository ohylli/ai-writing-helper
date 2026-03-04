using AIWritingHelper.Core;
using AIWritingHelper.UI;
using Xunit;

namespace AIWritingHelper.Tests.UI;

public class SettingsFormTests
{
    [Theory]
    [InlineData(Keys.Control, Keys.Space, "Ctrl+Space")]
    [InlineData(Keys.Control | Keys.Alt, Keys.Space, "Ctrl+Alt+Space")]
    [InlineData(Keys.Control | Keys.Alt, Keys.D, "Ctrl+Alt+D")]
    [InlineData(Keys.Control | Keys.Shift, Keys.F1, "Ctrl+Shift+F1")]
    [InlineData(Keys.Alt, Keys.F12, "Alt+F12")]
    [InlineData(Keys.Control | Keys.Alt | Keys.Shift, Keys.A, "Ctrl+Alt+Shift+A")]
    public void FormatHotkey_ProducesExpectedString(Keys modifiers, Keys keyCode, string expected)
    {
        var result = SettingsForm.FormatHotkey(modifiers, keyCode);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Keys.Control | Keys.Alt, Keys.Space)]
    [InlineData(Keys.Control | Keys.Alt, Keys.D)]
    [InlineData(Keys.Control | Keys.Shift, Keys.F1)]
    [InlineData(Keys.Alt, Keys.F12)]
    [InlineData(Keys.Control | Keys.Alt | Keys.Shift, Keys.A)]
    public void FormatHotkey_RoundTripsWithParseHotkey(Keys modifiers, Keys keyCode)
    {
        var formatted = SettingsForm.FormatHotkey(modifiers, keyCode);
        var (parsedMods, parsedVk) = GlobalHotkeyManager.ParseHotkey(formatted);

        // Convert Keys modifiers to Win32 modifier flags for comparison
        uint expectedMods = 0;
        if ((modifiers & Keys.Control) != 0) expectedMods |= 0x0002; // MOD_CONTROL
        if ((modifiers & Keys.Alt) != 0) expectedMods |= 0x0001;     // MOD_ALT
        if ((modifiers & Keys.Shift) != 0) expectedMods |= 0x0004;   // MOD_SHIFT

        Assert.Equal(expectedMods, parsedMods);
        Assert.Equal((uint)keyCode, parsedVk);
    }
}
