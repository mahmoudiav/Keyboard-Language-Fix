using System.Runtime.InteropServices;
using Clipboard = System.Windows.Clipboard;

namespace KeyboardLanguageFix.App;

/// <summary>
/// Clipboard access that tolerates the clipboard being briefly locked.
/// </summary>
/// <remarks>
/// Only one process may own the clipboard at a time, so any call can fail with
/// CLIPBRD_E_CANT_OPEN when another app happens to be reading it. Every
/// operation here retries a few times before giving up rather than throwing at
/// the user.
/// </remarks>
internal static class ClipboardHelper
{
    private const int Attempts = 8;
    private const int RetryDelayMs = 25;

    /// <summary>The clipboard's current text, or <c>null</c> when it holds none.</summary>
    internal static string? TryGetText()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (COMException)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
        return null;
    }

    /// <summary>Puts <paramref name="text"/> on the clipboard. Returns whether it worked.</summary>
    internal static bool TrySetText(string text)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) Clipboard.Clear();
                else Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
        return false;
    }

    /// <summary>Empties the clipboard, ignoring failures.</summary>
    internal static void TryClear()
    {
        try
        {
            Clipboard.Clear();
        }
        catch (COMException)
        {
            // Nothing useful to do; the next copy overwrites it anyway.
        }
        catch (ExternalException)
        {
        }
    }
}
