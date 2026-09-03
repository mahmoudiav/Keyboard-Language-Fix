using Microsoft.Win32;

namespace KeyboardLanguageFix.App;

/// <summary>
/// The "Fix keyboard language" entry in the Windows right-click menu.
/// </summary>
/// <remarks>
/// This is a plain shell verb: a key under <c>HKCU\Software\Classes</c> naming
/// the command Explorer should run, with <c>%1</c> standing for the file that
/// was clicked. Nothing is installed, no COM object is registered, and because
/// it is written under HKEY_CURRENT_USER it needs no administrator rights and
/// touches nobody else's account.
///
/// Two things it deliberately is not:
///
/// It is not a menu on selected *text*. Windows has no registry key for that —
/// every program builds its own text context menu, and none of them consult
/// the registry. Selected text is what the keyboard shortcut is for.
///
/// It is not a Windows 11 "modern" menu entry. Those require an IExplorerCommand
/// COM server shipped inside a packaged app, so on Windows 11 this appears
/// under "Show more options" (Shift+F10 opens that menu directly).
///
/// The verb is registered against the file types where converting the whole
/// file makes sense: whatever Windows perceives as plain text, plus a few note
/// formats that often carry no perceived type of their own.
/// </remarks>
internal static class ShellMenu
{
    /// <summary>The command-line switch Explorer invokes the app with.</summary>
    internal const string ConvertFileSwitch = "--convert-file";

    /// <summary>What the menu entry says.</summary>
    internal const string MenuText = "Fix keyboard language";

    private const string VerbName = "KeyboardLanguageFix";

    private static readonly string[] FileTypes =
    {
        @"SystemFileAssociations\text",
        @"SystemFileAssociations\.md",
        @"SystemFileAssociations\.csv",
        @"SystemFileAssociations\.json",
        @"SystemFileAssociations\.srt"
    };

    /// <summary>
    /// Whether this app can own the menu entry at all. A packaged (Store) install
    /// cannot: its writes to Software\Classes are redirected into the package,
    /// where Explorer never looks, so a verb has to be declared in the package
    /// manifest instead.
    /// </summary>
    internal static bool IsSupported => !PackageInfo.IsPackaged;

    /// <summary>Whether the entry is registered and still points at this copy of the app.</summary>
    internal static bool IsRegistered()
    {
        if (!IsSupported) return false;

        var expected = CommandLine();
        if (expected is null) return false;

        try
        {
            // Every file type, not just the first: an older version registered a
            // different set, and a half-written registration has to be noticed
            // and rewritten rather than passed over as good enough.
            foreach (var fileType in FileTypes)
            {
                using var key = Registry.CurrentUser.OpenSubKey(CommandKey(fileType));
                if (key?.GetValue(null) as string != expected) return false;
            }
            return true;
        }
        catch (Exception exception) when (IsRegistryDenial(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the entry exists at all, even if it points somewhere else. Used
    /// when removing it, so a key left behind by an older install still goes.
    /// </summary>
    internal static bool IsPresent()
    {
        if (!IsSupported) return false;

        try
        {
            foreach (var fileType in FileTypes)
            {
                using var key = Registry.CurrentUser.OpenSubKey(VerbKey(fileType));
                if (key is not null) return true;
            }
            return false;
        }
        catch (Exception exception) when (IsRegistryDenial(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Adds or removes the entry.
    /// </summary>
    /// <returns>
    /// Whether the change was made. False means the registry refused it, or this
    /// is a packaged install where the menu is not ours to add.
    /// </returns>
    internal static bool TrySetEnabled(bool enabled)
    {
        if (!IsSupported) return false;

        var command = CommandLine();
        if (enabled && command is null) return false;

        try
        {
            foreach (var fileType in FileTypes)
            {
                if (enabled) Write(fileType, command!);
                else Remove(fileType);
            }
            return true;
        }
        catch (Exception exception) when (IsRegistryDenial(exception))
        {
            return false;
        }
    }

    private static void Write(string fileType, string command)
    {
        using (var verb = Registry.CurrentUser.CreateSubKey(VerbKey(fileType), writable: true))
        {
            if (verb is null) return;
            verb.SetValue(null, MenuText);
            // Explorer draws the app's own icon beside the entry.
            verb.SetValue("Icon", $"\"{Environment.ProcessPath}\",0");
        }

        using var key = Registry.CurrentUser.CreateSubKey(CommandKey(fileType), writable: true);
        key?.SetValue(null, command);
    }

    private static void Remove(string fileType)
    {
        // Only ever our own verb key, never the shell key above it: other
        // programs put their verbs in the same place.
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey(fileType), throwOnMissingSubKey: false);
    }

    /// <summary>The command Explorer runs, with %1 standing for the clicked file.</summary>
    private static string? CommandLine()
    {
        var exePath = Environment.ProcessPath;
        return string.IsNullOrEmpty(exePath)
            ? null
            : $"\"{exePath}\" {ConvertFileSwitch} \"%1\"";
    }

    private static string VerbKey(string fileType) =>
        $@"Software\Classes\{fileType}\shell\{VerbName}";

    private static string CommandKey(string fileType) => VerbKey(fileType) + @"\command";

    private static bool IsRegistryDenial(Exception exception) =>
        exception is System.Security.SecurityException or UnauthorizedAccessException;
}
