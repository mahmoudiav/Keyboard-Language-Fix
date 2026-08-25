using Microsoft.Win32;

namespace KeyboardLanguageFix.App;

/// <summary>
/// Turning "start with Windows" on and off.
/// </summary>
/// <remarks>
/// The two install shapes need different mechanisms. An MSIX package declares a
/// <c>windows.startupTask</c> extension, and Windows — not the app — owns the
/// switch, which is why the packaged path opens the Startup Apps settings page
/// instead of flipping it silently. A plain copy of the exe uses the classic
/// Run key.
/// </remarks>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardLanguageFix";

    /// <summary>Whether Windows is set to start the app at sign-in.</summary>
    internal static bool IsEnabled()
    {
        if (PackageInfo.IsPackaged) return false; // Windows owns this; we cannot read it reliably

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException
                                              or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the requested setting for an unpackaged install.
    /// </summary>
    /// <returns>Whether the change was made here; false means the user must use Windows Settings.</returns>
    internal static bool TrySetEnabled(bool enabled)
    {
        if (PackageInfo.IsPackaged) return false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException
                                              or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Opens the Windows "Startup apps" settings page.</summary>
    internal static void OpenWindowsStartupSettings() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ms-settings:startupapps",
            UseShellExecute = true
        });
}
