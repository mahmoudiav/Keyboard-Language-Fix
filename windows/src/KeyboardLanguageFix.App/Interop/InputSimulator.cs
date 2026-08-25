using System.Runtime.InteropServices;
using static KeyboardLanguageFix.App.Interop.NativeMethods;

namespace KeyboardLanguageFix.App.Interop;

/// <summary>Sends synthetic keystrokes to whichever window has focus.</summary>
internal static class InputSimulator
{
    private static readonly int InputSize = Marshal.SizeOf<Input>();

    /// <summary>The modifier keys a user might be holding when a hotkey fires.</summary>
    private static readonly ushort[] ModifierKeys =
    {
        VkLShift, VkRShift, VkShift,
        VkLControl, VkRControl, VkControl,
        VkLMenu, VkRMenu, VkMenu,
        VkLWin, VkRWin
    };

    private static Input KeyDown(ushort virtualKey) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey } }
    };

    private static Input KeyUp(ushort virtualKey) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = KeyEventKeyUp }
        }
    };

    private static Input Unicode(char unit, bool up) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = unit,
                Flags = KeyEventUnicode | (up ? KeyEventKeyUp : 0)
            }
        }
    };

    private static void Send(params Input[] inputs)
    {
        if (inputs.Length == 0) return;
        SendInput((uint)inputs.Length, inputs, InputSize);
    }

    /// <summary>
    /// Tells Windows the modifier keys are up.
    /// </summary>
    /// <remarks>
    /// The user is still physically holding the hotkey when this runs. Sending
    /// Ctrl+C on top of a held Shift would reach the app as Ctrl+Shift+C — a
    /// completely different command — so every modifier is released first.
    /// They are not pressed again afterwards: the user's own key-up does that.
    /// </remarks>
    internal static void ReleaseHeldModifiers()
    {
        var held = ModifierKeys
            .Where(key => (GetAsyncKeyState(key) & 0x8000) != 0)
            .Select(KeyUp)
            .ToArray();

        Send(held);
    }

    /// <summary>Sends Ctrl+C.</summary>
    internal static void SendCopy() => SendCtrl(VkC);

    /// <summary>Sends Ctrl+V.</summary>
    internal static void SendPaste() => SendCtrl(VkV);

    private static void SendCtrl(ushort virtualKey) => Send(
        KeyDown(VkControl),
        KeyDown(virtualKey),
        KeyUp(virtualKey),
        KeyUp(VkControl));

    /// <summary>
    /// Types <paramref name="text"/> character by character, bypassing the
    /// clipboard entirely. Slower than pasting, but leaves the clipboard alone.
    /// </summary>
    internal static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Each UTF-16 code unit is sent separately, so surrogate pairs
        // (and therefore anything outside the BMP) survive intact.
        var inputs = new List<Input>(text.Length * 2);
        foreach (var unit in text)
        {
            inputs.Add(Unicode(unit, up: false));
            inputs.Add(Unicode(unit, up: true));
        }

        // SendInput is capped in practice; send in batches so long text works.
        const int batch = 200;
        for (var offset = 0; offset < inputs.Count; offset += batch)
        {
            Send(inputs.Skip(offset).Take(batch).ToArray());
        }
    }
}
