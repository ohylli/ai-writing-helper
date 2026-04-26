using System.Runtime.InteropServices;

namespace AIWritingHelper.Core;

internal sealed class Win32InputSimulator : IInputSimulator
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    public void SendPaste()
    {
        var inputs = new INPUT[]
        {
            MakeKey(VK_CONTROL, keyUp: false),
            MakeKey(VK_V, keyUp: false),
            MakeKey(VK_V, keyUp: true),
            MakeKey(VK_CONTROL, keyUp: true),
        };

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            // Most common cause: the focused window belongs to an elevated process
            // and we are running unelevated, so UIPI rejects the batch.
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Could not paste — the focused window may require elevated privileges (Win32 error {err}, delivered {sent}/{inputs.Length} events)");
        }
    }

    private static INPUT MakeKey(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        union = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
