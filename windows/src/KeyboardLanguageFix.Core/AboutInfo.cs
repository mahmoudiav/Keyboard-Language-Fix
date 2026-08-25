namespace KeyboardLanguageFix.Core;

/// <summary>
/// The product's identity, in one place.
/// </summary>
/// <remarks>
/// Deliberately English-only: the interface follows the user's Windows
/// language, but a person's name, an address and a licence should read the
/// same everywhere they are quoted.
/// </remarks>
public static class AboutInfo
{
    /// <summary>The product name as people should see it.</summary>
    public const string ProductName = "Keyboard Language Fix";

    /// <summary>One line describing what the app does.</summary>
    public const string Tagline =
        "Typed in the wrong keyboard language? Select the text, press one shortcut, " +
        "and it is re-typed in the language you meant.";

    /// <summary>Who made it.</summary>
    public const string Author = "Mahmoud SATALEH";

    /// <summary>Credit line shown in the About box and the installer.</summary>
    public const string Credit = "Idea and implementation: " + Author;

    /// <summary>Where to write with problems or ideas.</summary>
    public const string Email = "mahmoudiav@icloud.com";

    /// <summary>Price, stated plainly because people ask.</summary>
    public const string Pricing = "Free software — free to use and free to share.";

    /// <summary>The licence the source is published under.</summary>
    public const string License = "Released under the MIT License.";

    /// <summary>
    /// The running build's version, taken from the assembly so the About box can
    /// never disagree with what was actually shipped.
    /// </summary>
    public static string Version
    {
        get
        {
            var version = typeof(AboutInfo).Assembly.GetName().Version;
            return version is null
                ? "1.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>Copyright line, e.g. for the executable's file properties.</summary>
    public static string Copyright => $"Copyright © {Author}";
}
