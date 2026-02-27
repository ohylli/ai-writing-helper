using AIWritingHelper.Core;
using Xunit;

namespace AIWritingHelper.Tests.Core;

public class GlobalHotkeyManagerTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", 0x0001 | 0x0002, (uint)Keys.Space)]
    [InlineData("Ctrl+Alt+D", 0x0001 | 0x0002, (uint)Keys.D)]
    [InlineData("Ctrl+Shift+F1", 0x0002 | 0x0004, (uint)Keys.F1)]
    [InlineData("Alt+F12", 0x0001, (uint)Keys.F12)]
    [InlineData("Ctrl+Alt+Shift+A", 0x0001 | 0x0002 | 0x0004, (uint)Keys.A)]
    [InlineData("Win+E", 0x0008, (uint)Keys.E)]
    public void ParseHotkey_ValidCombinations_ReturnsParsed(string input, uint expectedMods, uint expectedVk)
    {
        var (modifiers, vk) = GlobalHotkeyManager.ParseHotkey(input);

        Assert.Equal(expectedMods, modifiers);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("ctrl+alt+space")]
    [InlineData("CTRL+ALT+SPACE")]
    [InlineData("Ctrl+Alt+space")]
    public void ParseHotkey_CaseInsensitive(string input)
    {
        var (modifiers, vk) = GlobalHotkeyManager.ParseHotkey(input);

        Assert.Equal((uint)(0x0001 | 0x0002), modifiers);
        Assert.Equal((uint)Keys.Space, vk);
    }

    [Fact]
    public void ParseHotkey_ControlAlternateSpelling()
    {
        var (modifiers, vk) = GlobalHotkeyManager.ParseHotkey("Control+Alt+Space");

        Assert.Equal((uint)(0x0001 | 0x0002), modifiers);
        Assert.Equal((uint)Keys.Space, vk);
    }

    [Fact]
    public void ParseHotkey_WhitespaceTrimmed()
    {
        var (modifiers, vk) = GlobalHotkeyManager.ParseHotkey("  Ctrl + Alt + Space  ");

        Assert.Equal((uint)(0x0001 | 0x0002), modifiers);
        Assert.Equal((uint)Keys.Space, vk);
    }

    [Theory]
    [InlineData("Ctrl+F1", 0x0002, (uint)Keys.F1)]
    [InlineData("Alt+F5", 0x0001, (uint)Keys.F5)]
    public void ParseHotkey_FunctionKeys(string input, uint expectedMods, uint expectedVk)
    {
        var (modifiers, vk) = GlobalHotkeyManager.ParseHotkey(input);

        Assert.Equal(expectedMods, modifiers);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseHotkey_NullOrEmpty_Throws(string? input)
    {
        Assert.Throws<ArgumentException>(() => GlobalHotkeyManager.ParseHotkey(input!));
    }

    [Fact]
    public void ParseHotkey_SingleToken_Throws()
    {
        Assert.Throws<ArgumentException>(() => GlobalHotkeyManager.ParseHotkey("Space"));
    }

    [Fact]
    public void ParseHotkey_NoModifier_Throws()
    {
        // "Space+A" — Space is not a modifier
        Assert.Throws<ArgumentException>(() => GlobalHotkeyManager.ParseHotkey("Space+A"));
    }

    [Fact]
    public void ParseHotkey_UnknownModifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => GlobalHotkeyManager.ParseHotkey("Meta+A"));
    }

    [Fact]
    public void ParseHotkey_UnknownKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => GlobalHotkeyManager.ParseHotkey("Ctrl+NotAKey"));
    }
}
