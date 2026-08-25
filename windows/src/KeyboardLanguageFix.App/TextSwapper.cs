using KeyboardLanguageFix.App.Interop;
using KeyboardLanguageFix.Core;
using static KeyboardLanguageFix.App.Interop.NativeMethods;

namespace KeyboardLanguageFix.App;

/// <summary>Why a swap did not happen.</summary>
internal enum SwapStatus
{
    /// <summary>The selected text was replaced.</summary>
    Converted,

    /// <summary>Nothing was selected, or the app did not answer the copy.</summary>
    NothingSelected,

    /// <summary>The text was already in the right language.</summary>
    NothingToChange,

    /// <summary>The clipboard could not be written.</summary>
    ClipboardUnavailable
}

/// <summary>The outcome of one hotkey press.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Text">The converted text, when there is one.</param>
/// <param name="LayoutId">The layout that was applied.</param>
internal readonly record struct SwapOutcome(SwapStatus Status, string? Text, string? LayoutId);

/// <summary>
/// Reads the current selection out of whatever app has focus, converts it, and
/// puts it back.
/// </summary>
/// <remarks>
/// Windows offers no way to read another application's selection directly, so
/// this does what every tool of this kind does: it asks the app to copy, reads
/// the clipboard, and pastes the replacement. The clipboard is put back
/// afterwards.
/// </remarks>
internal sealed class TextSwapper
{
    private readonly Func<AppSettings> _settings;

    internal TextSwapper(Func<AppSettings> settings) => _settings = settings;

    /// <summary>Runs one copy-convert-replace cycle against the foreground window.</summary>
    internal async Task<SwapOutcome> SwapAsync()
    {
        var settings = _settings();

        // The user is still holding the hotkey; let go of it on their behalf so
        // our Ctrl+C is not read as Ctrl+Shift+C.
        InputSimulator.ReleaseHeldModifiers();
        await Task.Delay(20).ConfigureAwait(true);

        var savedClipboard = ClipboardHelper.TryGetText();
        var sequenceBefore = GetClipboardSequenceNumber();

        // Empty it first, so a stale clipboard cannot be mistaken for a fresh copy.
        ClipboardHelper.TryClear();
        InputSimulator.SendCopy();

        var selection = await WaitForCopyAsync(sequenceBefore, settings.ClipboardTimeoutMs)
            .ConfigureAwait(true);

        if (string.IsNullOrEmpty(selection))
        {
            await RestoreAsync(savedClipboard, settings, immediate: true).ConfigureAwait(true);
            return new SwapOutcome(SwapStatus.NothingSelected, null, null);
        }

        var result = Converter.Convert(selection, settings.ToConversionOptions());
        if (!result.Changed)
        {
            await RestoreAsync(savedClipboard, settings, immediate: true).ConfigureAwait(true);
            return new SwapOutcome(SwapStatus.NothingToChange, null, result.LayoutId);
        }

        if (settings.ReplaceMethod == ReplaceMethod.Type)
        {
            // Put the clipboard back before typing: we are not going to use it.
            await RestoreAsync(savedClipboard, settings, immediate: true).ConfigureAwait(true);
            InputSimulator.TypeText(result.Text);
            return new SwapOutcome(SwapStatus.Converted, result.Text, result.LayoutId);
        }

        if (!ClipboardHelper.TrySetText(result.Text))
        {
            await RestoreAsync(savedClipboard, settings, immediate: true).ConfigureAwait(true);
            return new SwapOutcome(SwapStatus.ClipboardUnavailable, null, result.LayoutId);
        }

        InputSimulator.SendPaste();
        await RestoreAsync(savedClipboard, settings, immediate: false).ConfigureAwait(true);

        return new SwapOutcome(SwapStatus.Converted, result.Text, result.LayoutId);
    }

    /// <summary>
    /// Waits for the foreground app to answer our Ctrl+C, polling the clipboard
    /// sequence number rather than sleeping for a fixed guess.
    /// </summary>
    private static async Task<string?> WaitForCopyAsync(uint sequenceBefore, int timeoutMs)
    {
        const int pollMs = 15;
        var waited = 0;

        while (waited < timeoutMs)
        {
            await Task.Delay(pollMs).ConfigureAwait(true);
            waited += pollMs;

            if (GetClipboardSequenceNumber() == sequenceBefore) continue;

            var text = ClipboardHelper.TryGetText();
            if (!string.IsNullOrEmpty(text)) return text;
        }

        // One last look: some apps update the clipboard without bumping the
        // sequence number in a way we observe in time.
        return ClipboardHelper.TryGetText();
    }

    /// <summary>
    /// Puts the user's clipboard back.
    /// </summary>
    /// <param name="immediate">
    /// False when a paste was just sent: the target app needs a moment to read
    /// the clipboard before we overwrite it again.
    /// </param>
    private static async Task RestoreAsync(string? saved, AppSettings settings, bool immediate)
    {
        if (!settings.RestoreClipboard) return;

        if (!immediate) await Task.Delay(250).ConfigureAwait(true);

        if (saved is null) ClipboardHelper.TryClear();
        else ClipboardHelper.TrySetText(saved);
    }
}
