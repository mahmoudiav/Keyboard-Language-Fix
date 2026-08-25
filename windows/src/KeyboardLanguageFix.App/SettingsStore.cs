using System.IO;
using System.Text;
using KeyboardLanguageFix.Core;
using static KeyboardLanguageFix.App.Interop.NativeMethods;

namespace KeyboardLanguageFix.App;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON under LocalAppData.</summary>
internal sealed class SettingsStore
{
    private readonly string _path;

    internal SettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyboardLanguageFix");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
    }

    /// <summary>Where the settings file lives.</summary>
    internal string FilePath => _path;

    /// <summary>
    /// Reads the settings. A missing, unreadable or malformed file yields the
    /// defaults rather than an error: the app must always be able to start.
    /// </summary>
    internal AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? AppSettings.FromJson(File.ReadAllText(_path, Encoding.UTF8))
                : new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Writes the settings. Returns whether it worked.</summary>
    internal bool Save(AppSettings settings)
    {
        try
        {
            // Write beside the target then swap, so a crash mid-write cannot
            // leave a truncated file behind.
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, settings.Normalised().ToJson(), Encoding.UTF8);
            File.Move(temporary, _path, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Tells whether the app is running from an MSIX package.</summary>
internal static class PackageInfo
{
    private static readonly Lazy<bool> PackagedValue = new(Detect);

    /// <summary>True when running from an MSIX package (a Microsoft Store install).</summary>
    internal static bool IsPackaged => PackagedValue.Value;

    private static bool Detect()
    {
        try
        {
            var length = 0;
            // Called with a null buffer this reports the required length, or
            // APPMODEL_ERROR_NO_PACKAGE when there is no package at all.
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch (EntryPointNotFoundException)
        {
            return false; // pre-Windows 8; no packaging to speak of
        }
    }
}
