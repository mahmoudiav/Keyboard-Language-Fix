using System.Runtime.InteropServices;

namespace KeyboardLanguageFix.App.Interop;

/// <summary>The Win32 surface this app needs. Nothing here is exposed further up.</summary>
internal static class NativeMethods
{
    // ---- Global hotkeys -------------------------------------------------

    internal const int WmHotkey = 0x0312;

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;

    /// <summary>Stops auto-repeat firing the hotkey while the user holds it down.</summary>
    internal const uint ModNoRepeat = 0x4000;

    /// <summary>A message-only window: never visible, still gets posted messages.</summary>
    internal static readonly IntPtr HwndMessage = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---- Synthetic input ------------------------------------------------

    internal const uint InputKeyboard = 1;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;

    internal const ushort VkControl = 0x11;
    internal const ushort VkShift = 0x10;
    internal const ushort VkMenu = 0x12;      // Alt
    internal const ushort VkLWin = 0x5B;
    internal const ushort VkRWin = 0x5C;
    internal const ushort VkLShift = 0xA0;
    internal const ushort VkRShift = 0xA1;
    internal const ushort VkLControl = 0xA2;
    internal const ushort VkRControl = 0xA3;
    internal const ushort VkLMenu = 0xA4;
    internal const ushort VkRMenu = 0xA5;
    internal const ushort VkC = 0x43;
    internal const ushort VkV = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal MouseInput Mouse;
        [FieldOffset(0)] internal KeyboardInput Keyboard;
        [FieldOffset(0)] internal HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint Data;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        internal uint Message;
        internal ushort ParamL;
        internal ushort ParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    // ---- Clipboard ------------------------------------------------------

    /// <summary>
    /// Bumped by Windows on every clipboard change, so we can tell when the app
    /// we are copying from has actually answered instead of guessing with a sleep.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    // ---- Packaging ------------------------------------------------------

    internal const int ErrorSuccess = 0;
    internal const int AppModelErrorNoPackage = 15700;

    /// <summary>Succeeds only when the process runs from an MSIX package.</summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int GetCurrentPackageFullName(ref int length, System.Text.StringBuilder? name);
}
