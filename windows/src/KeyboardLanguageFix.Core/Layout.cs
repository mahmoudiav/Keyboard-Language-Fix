using System.Text.RegularExpressions;

namespace KeyboardLanguageFix.Core;

/// <summary>
/// One keyboard layout, described as the characters its keys produce when the
/// same physical keys are read against a US QWERTY keyboard.
/// </summary>
public sealed class Layout
{
    private readonly Regex _script;

    /// <summary>Creates a layout from its two key layers.</summary>
    public Layout(
        string id,
        string name,
        string nameLocal,
        bool rightToLeft,
        bool shiftFallback,
        string scriptPattern,
        IReadOnlyDictionary<string, string> baseLayer,
        IReadOnlyDictionary<string, string> shiftLayer)
    {
        Id = id;
        Name = name;
        NameLocal = nameLocal;
        RightToLeft = rightToLeft;
        ShiftFallback = shiftFallback;
        BaseLayer = baseLayer;
        ShiftLayer = shiftLayer;
        _script = new Regex(scriptPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>Stable identifier, e.g. <c>ar</c>.</summary>
    public string Id { get; }

    /// <summary>English name, e.g. <c>Arabic (101)</c>.</summary>
    public string Name { get; }

    /// <summary>Name in the layout's own language.</summary>
    public string NameLocal { get; }

    /// <summary>Whether the script reads right to left.</summary>
    public bool RightToLeft { get; }

    /// <summary>
    /// True when the script has no upper case, so a shifted Latin letter should
    /// fall back to the un-shifted layer instead of being left alone.
    /// </summary>
    public bool ShiftFallback { get; }

    /// <summary>The un-shifted layer, keyed by the US QWERTY character.</summary>
    public IReadOnlyDictionary<string, string> BaseLayer { get; }

    /// <summary>The shifted layer, keyed by the US QWERTY character.</summary>
    public IReadOnlyDictionary<string, string> ShiftLayer { get; }

    /// <summary>Whether <paramref name="value"/> contains this layout's script.</summary>
    public bool MatchesScript(string value) => _script.IsMatch(value);

    /// <summary>Whether the single character <paramref name="value"/> is in this script.</summary>
    public bool MatchesScript(char value) => _script.IsMatch(value.ToString());

    /// <inheritdoc />
    public override string ToString() => $"{NameLocal} — {Name}";
}
