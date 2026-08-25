using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardLanguageFix.Core;

/// <summary>What to do when the conversion produces text for the foreground app.</summary>
public enum ReplaceMethod
{
    /// <summary>Put the text on the clipboard and send Ctrl+V. Fast, and correct for long text.</summary>
    Paste,

    /// <summary>Type the characters one by one. Slower, but never touches the clipboard.</summary>
    Type
}

/// <summary>A global hotkey, stored as Win32 virtual-key plus modifiers.</summary>
public sealed class HotkeySetting
{
    /// <summary>Win32 virtual-key code. Defaults to VK_SPACE.</summary>
    public int VirtualKey { get; set; } = 0x20;

    /// <summary>Whether Ctrl must be held.</summary>
    public bool Control { get; set; } = true;

    /// <summary>Whether Shift must be held.</summary>
    public bool Shift { get; set; } = true;

    /// <summary>Whether Alt must be held.</summary>
    public bool Alt { get; set; }

    /// <summary>Whether the Windows key must be held.</summary>
    public bool Windows { get; set; }

    /// <summary>A copy of this hotkey.</summary>
    public HotkeySetting Clone() => new()
    {
        VirtualKey = VirtualKey,
        Control = Control,
        Shift = Shift,
        Alt = Alt,
        Windows = Windows
    };

    /// <summary>True when at least one modifier is held; Windows rejects bare keys.</summary>
    [JsonIgnore]
    public bool HasModifier => Control || Alt || Windows;
}

/// <summary>Everything the user can configure, persisted as JSON.</summary>
public sealed class AppSettings
{
    /// <summary>Layout that Latin text is converted into.</summary>
    public string PrimaryLayout { get; set; } = "ar";

    /// <summary>Layouts recognised when converting back to Latin.</summary>
    public List<string> EnabledLayouts { get; set; } = new() { "ar" };

    /// <summary>How the direction of a conversion is chosen.</summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Auto;

    /// <summary>The global hotkey.</summary>
    public HotkeySetting Hotkey { get; set; } = new();

    /// <summary>How the replacement text reaches the foreground app.</summary>
    public ReplaceMethod ReplaceMethod { get; set; } = ReplaceMethod.Paste;

    /// <summary>Whether to put the previous clipboard contents back afterwards.</summary>
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>Whether to show a tray balloon after converting.</summary>
    public bool ShowNotifications { get; set; }

    /// <summary>Whether the app should start with Windows.</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>Per-layout key overrides, keyed by layout id.</summary>
    public Dictionary<string, Dictionary<string, string>> CustomMap { get; set; } = new();

    /// <summary>
    /// Milliseconds to wait for the foreground app to answer the copy we send it.
    /// Slow apps (remote desktops, Electron editors) may need more.
    /// </summary>
    public int ClipboardTimeoutMs { get; set; } = 400;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Conversion options matching these settings.</summary>
    public ConversionOptions ToConversionOptions() => new()
    {
        PrimaryLayout = PrimaryLayout,
        // The primary layout must always be recognisable, otherwise Auto mode
        // could never convert its script back to Latin.
        EnabledLayouts = EnabledLayouts.Contains(PrimaryLayout, StringComparer.OrdinalIgnoreCase)
            ? EnabledLayouts
            : EnabledLayouts.Append(PrimaryLayout).ToList(),
        Mode = Mode,
        CustomMap = CustomMap.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string>)pair.Value,
            StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>Serialises these settings as indented JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Reads settings from JSON. Anything missing or malformed falls back to the
    /// defaults, so a hand-edited or truncated file can never stop the app starting.
    /// </summary>
    public static AppSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)?.Normalised()
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Repairs values that would otherwise put the app in an unusable state.</summary>
    public AppSettings Normalised()
    {
        if (Layouts.Find(PrimaryLayout) is null) PrimaryLayout = "ar";

        EnabledLayouts = EnabledLayouts
            .Where(id => Layouts.Find(id) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (EnabledLayouts.Count == 0) EnabledLayouts.Add(PrimaryLayout);

        Hotkey ??= new HotkeySetting();
        // A hotkey with no modifier would swallow a plain key system-wide.
        if (!Hotkey.HasModifier) Hotkey = new HotkeySetting();

        ClipboardTimeoutMs = Math.Clamp(ClipboardTimeoutMs, 100, 5000);
        CustomMap ??= new Dictionary<string, Dictionary<string, string>>();

        return this;
    }
}
