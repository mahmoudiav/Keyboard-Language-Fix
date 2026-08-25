using System.Windows.Input;
using KeyboardLanguageFix.Core;

namespace KeyboardLanguageFix.App;

/// <summary>Turns a <see cref="HotkeySetting"/> into something a person can read.</summary>
internal static class HotkeyLabel
{
    /// <summary>For example <c>Ctrl + Shift + Space</c>.</summary>
    internal static string Describe(HotkeySetting hotkey)
    {
        if (hotkey is null) return "not set";

        var parts = new List<string>(4);
        if (hotkey.Control) parts.Add("Ctrl");
        if (hotkey.Alt) parts.Add("Alt");
        if (hotkey.Shift) parts.Add("Shift");
        if (hotkey.Windows) parts.Add("Win");
        parts.Add(KeyName(hotkey.VirtualKey));

        return string.Join(" + ", parts);
    }

    /// <summary>A display name for a Win32 virtual-key code.</summary>
    internal static string KeyName(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key switch
        {
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Oem1 => ";",
            Key.OemQuestion => "/",
            Key.OemTilde => "`",
            Key.OemOpenBrackets => "[",
            Key.Oem6 => "]",
            Key.Oem7 => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemBackslash or Key.Oem5 => "\\",
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            Key.None => $"0x{virtualKey:X2}",
            _ => key.ToString()
        };
    }
}
