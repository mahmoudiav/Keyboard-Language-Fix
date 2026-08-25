using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KeyboardLanguageFix.Core;
using static KeyboardLanguageFix.App.Interop.NativeMethods;

namespace KeyboardLanguageFix.App.Interop;

/// <summary>
/// Owns the process-wide hotkey.
/// </summary>
/// <remarks>
/// This uses <c>RegisterHotKey</c> rather than a low-level keyboard hook. The
/// app therefore never sees a keystroke it was not registered for, which keeps
/// it out of the "records everything you type" category — a distinction that
/// matters both for the user and for Store certification.
/// </remarks>
internal sealed class HotkeyListener : IDisposable
{
    private const int HotkeyId = 0xA71;

    private readonly HwndSource _source;
    private bool _registered;
    private bool _disposed;

    /// <summary>Raised on the UI thread when the hotkey is pressed.</summary>
    internal event EventHandler? Pressed;

    internal HotkeyListener()
    {
        // A message-only window: no pixels, no taskbar entry, still pumps messages.
        _source = new HwndSource(new HwndSourceParameters("KeyboardLanguageFixHotkey")
        {
            ParentWindow = HwndMessage,
            Width = 0,
            Height = 0
        });
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// Registers <paramref name="hotkey"/>, replacing any previous one.
    /// </summary>
    /// <returns>
    /// <c>null</c> on success, or a description of why Windows refused — most
    /// often another app already owns the combination.
    /// </returns>
    internal string? Register(HotkeySetting hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        Unregister();

        if (!hotkey.HasModifier)
        {
            return "A shortcut needs Ctrl, Alt or the Windows key.";
        }

        var modifiers = ModNoRepeat;
        if (hotkey.Control) modifiers |= ModControl;
        if (hotkey.Shift) modifiers |= ModShift;
        if (hotkey.Alt) modifiers |= ModAlt;
        if (hotkey.Windows) modifiers |= ModWin;

        if (RegisterHotKey(_source.Handle, HotkeyId, modifiers, (uint)hotkey.VirtualKey))
        {
            _registered = true;
            return null;
        }

        var error = Marshal.GetLastWin32Error();
        return error == 1409 // ERROR_HOTKEY_ALREADY_REGISTERED
            ? "Another application is already using that shortcut."
            : new Win32Exception(error).Message;
    }

    private void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey || wParam.ToInt32() != HotkeyId) return IntPtr.Zero;
        handled = true;
        Pressed?.Invoke(this, EventArgs.Empty);
        return IntPtr.Zero;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
