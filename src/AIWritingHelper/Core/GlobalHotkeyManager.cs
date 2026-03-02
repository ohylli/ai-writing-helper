using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AIWritingHelper.Core;

public sealed class GlobalHotkeyManager : IDisposable
{
    internal const int TypoFixHotkeyId = 1;
    internal const int DictationHotkeyId = 2;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly ILogger<GlobalHotkeyManager> _logger;
    private readonly HotkeyWindow _window;
    private readonly HashSet<int> _registeredIds = new();
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public GlobalHotkeyManager(ILogger<GlobalHotkeyManager> logger)
    {
        _logger = logger;
        _window = new HotkeyWindow(this);
    }

    public bool Register(int id, string hotkeyString)
    {
        if (_registeredIds.Contains(id))
            throw new InvalidOperationException($"Hotkey ID {id} is already registered.");

        var (modifiers, vk) = ParseHotkey(hotkeyString);
        modifiers |= MOD_NOREPEAT;

        if (!RegisterHotKey(_window.Handle, id, modifiers, vk))
        {
            _logger.LogWarning("Failed to register hotkey {Hotkey} (id {Id}) — may be in use by another application",
                hotkeyString, id);
            return false;
        }

        _registeredIds.Add(id);
        _logger.LogInformation("Registered hotkey {Hotkey} (id {Id})", hotkeyString, id);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredIds)
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _registeredIds.Clear();
    }

    internal static (uint modifiers, uint vk) ParseHotkey(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            throw new ArgumentException("Hotkey string cannot be null or empty.", nameof(hotkeyString));

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ArgumentException($"Hotkey must have at least one modifier and a key: '{hotkeyString}'", nameof(hotkeyString));

        uint modifiers = 0;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => MOD_CONTROL,
                "ALT" => MOD_ALT,
                "SHIFT" => MOD_SHIFT,
                "WIN" => MOD_WIN,
                _ => throw new ArgumentException($"Unknown modifier: '{parts[i]}'", nameof(hotkeyString))
            };
        }

        var keyPart = parts[^1];
        if (!Enum.TryParse<Keys>(keyPart, ignoreCase: true, out var key))
            throw new ArgumentException($"Unknown key: '{keyPart}'", nameof(hotkeyString));

        return (modifiers, (uint)key);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _window.DestroyHandle();
    }

    private sealed class HotkeyWindow : NativeWindow
    {
        private readonly GlobalHotkeyManager _owner;

        public HotkeyWindow(GlobalHotkeyManager owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                _owner.HotkeyPressed?.Invoke(_owner, new HotkeyPressedEventArgs(id));
            }
            base.WndProc(ref m);
        }
    }
}

public class HotkeyPressedEventArgs : EventArgs
{
    public int HotkeyId { get; }

    public HotkeyPressedEventArgs(int hotkeyId)
    {
        HotkeyId = hotkeyId;
    }
}
