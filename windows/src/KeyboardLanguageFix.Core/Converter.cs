using System.Collections.Concurrent;
using System.Text;

namespace KeyboardLanguageFix.Core;

/// <summary>Which way a conversion ran.</summary>
public enum ConversionDirection
{
    /// <summary>Latin keystrokes were turned into the target layout.</summary>
    ToLayout,

    /// <summary>Target-layout characters were turned back into Latin.</summary>
    ToLatin
}

/// <summary>How the direction of a conversion is chosen.</summary>
public enum ConversionMode
{
    /// <summary>Decide from the text itself.</summary>
    Auto,

    /// <summary>Always convert Latin into the primary layout.</summary>
    ToLayout,

    /// <summary>Always convert back to Latin.</summary>
    ToLatin
}

/// <summary>The outcome of a conversion.</summary>
/// <param name="Text">The converted text, or the original when nothing changed.</param>
/// <param name="Changed">Whether anything actually changed.</param>
/// <param name="Direction">Which way the conversion ran.</param>
/// <param name="LayoutId">The layout that was used.</param>
public readonly record struct ConversionResult(
    string Text,
    bool Changed,
    ConversionDirection Direction,
    string? LayoutId);

/// <summary>Settings that shape a single conversion.</summary>
public sealed class ConversionOptions
{
    /// <summary>Layout that Latin text is converted into.</summary>
    public string PrimaryLayout { get; set; } = "ar";

    /// <summary>Layouts considered when converting non-Latin text back to Latin.</summary>
    public IReadOnlyList<string> EnabledLayouts { get; set; } = new[] { "ar" };

    /// <summary>How the direction is chosen.</summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Auto;

    /// <summary>Per-layout key overrides, keyed by layout id.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? CustomMap { get; set; }
}

/// <summary>
/// Re-types text through a keyboard layout table: the same physical keys, read
/// as if the other layout had been active.
/// </summary>
/// <remarks>
/// This mirrors <c>src/core/converter.js</c> in the browser extension. Both are
/// driven by the same generated tables, so they behave identically.
/// </remarks>
public static class Converter
{
    private static readonly ConcurrentDictionary<string, KeyMaps> Cache = new(StringComparer.Ordinal);

    /// <summary>Forgets the compiled tables; call after custom mappings change.</summary>
    public static void Invalidate() => Cache.Clear();

    /// <summary>Converts <paramref name="text"/> according to <paramref name="options"/>.</summary>
    public static ConversionResult Convert(string? text, ConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var primary = Layouts.Find(options.PrimaryLayout);
        if (string.IsNullOrEmpty(text) || primary is null)
        {
            return new ConversionResult(text ?? string.Empty, false, ConversionDirection.ToLayout, null);
        }

        var enabled = options.EnabledLayouts is { Count: > 0 }
            ? options.EnabledLayouts
            : new[] { primary.Id };

        ConversionDirection direction;
        Layout layout;

        switch (options.Mode)
        {
            case ConversionMode.ToLayout:
                direction = ConversionDirection.ToLayout;
                layout = primary;
                break;

            case ConversionMode.ToLatin:
                direction = ConversionDirection.ToLatin;
                layout = DetectLayout(text, enabled) ?? primary;
                break;

            default:
                var detected = DetectLayout(text, enabled);
                var (latin, target) = Score(text, detected ?? primary);
                if (detected is not null && target >= latin)
                {
                    direction = ConversionDirection.ToLatin;
                    layout = detected;
                }
                else
                {
                    direction = ConversionDirection.ToLayout;
                    layout = primary;
                }
                break;
        }

        IReadOnlyDictionary<string, string>? overrides = null;
        options.CustomMap?.TryGetValue(layout.Id, out overrides);

        var maps = GetMaps(layout, overrides);
        var converted = direction == ConversionDirection.ToLayout
            ? Apply(text, maps.ToLayout, maps.ToLayoutWidths)
            : Apply(text, maps.ToLatin, maps.ToLatinWidths);

        return new ConversionResult(
            converted,
            !string.Equals(converted, text, StringComparison.Ordinal),
            direction,
            layout.Id);
    }

    /// <summary>
    /// Picks the layout a piece of non-Latin text was most likely typed in, or
    /// <c>null</c> when it carries no recognisable script.
    /// </summary>
    public static Layout? DetectLayout(string text, IReadOnlyList<string>? candidateIds = null)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var candidates = candidateIds is { Count: > 0 }
            ? candidateIds.Select(Layouts.Find).OfType<Layout>()
            : Layouts.All;

        Layout? best = null;
        var bestHits = 0;

        foreach (var layout in candidates)
        {
            var hits = text.Count(layout.MatchesScript);
            if (hits > 0 && hits > bestHits)
            {
                best = layout;
                bestHits = hits;
            }
        }

        return best;
    }

    /// <summary>Counts Latin letters against the layout's own script; everything else is ignored.</summary>
    private static (int Latin, int Target) Score(string text, Layout layout)
    {
        var latin = 0;
        var target = 0;
        foreach (var ch in text)
        {
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z') latin++;
            else if (layout.MatchesScript(ch)) target++;
        }
        return (latin, target);
    }

    private static string Apply(string text, IReadOnlyDictionary<string, string> map, IReadOnlyList<int> widths)
    {
        var result = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var matched = false;

            // Multi-character keys first, longest wins, so the Arabic "laa"
            // ligature beats its own first letter.
            foreach (var width in widths)
            {
                if (index + width > text.Length) continue;
                var chunk = text.Substring(index, width);
                if (!map.TryGetValue(chunk, out var replacement)) continue;
                result.Append(replacement);
                index += width;
                matched = true;
                break;
            }

            if (matched) continue;

            var single = text[index].ToString();
            result.Append(map.TryGetValue(single, out var value) ? value : single);
            index++;
        }

        return result.ToString();
    }

    private static KeyMaps GetMaps(Layout layout, IReadOnlyDictionary<string, string>? overrides)
    {
        var key = overrides is null || overrides.Count == 0
            ? layout.Id
            : layout.Id + "|" + string.Join(
                ";", overrides.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value));

        return Cache.GetOrAdd(key, _ => Compile(layout, overrides));
    }

    private static KeyMaps Compile(Layout layout, IReadOnlyDictionary<string, string>? overrides)
    {
        var toLayout = new Dictionary<string, string>(StringComparer.Ordinal);
        var toLatin = new Dictionary<string, string>(StringComparer.Ordinal);

        void Put(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            toLayout[key] = value;
            // First writer wins, so the un-shifted layer is preferred when reversing.
            toLatin.TryAdd(value, key);
        }

        foreach (var (key, value) in layout.BaseLayer) Put(key, value);
        foreach (var (key, value) in layout.ShiftLayer) Put(key, value);

        if (layout.ShiftFallback)
        {
            foreach (var (key, value) in layout.BaseLayer)
            {
                var upper = key.ToUpperInvariant();
                if (!string.Equals(upper, key, StringComparison.Ordinal))
                {
                    toLayout.TryAdd(upper, value);
                }
            }
        }

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;
                toLayout[key] = value;
                toLatin[value] = key; // user overrides win in both directions
            }
        }

        return new KeyMaps(toLayout, toLatin, Widths(toLayout.Keys), Widths(toLatin.Keys));
    }

    /// <summary>Descending key lengths greater than one, for greedy matching.</summary>
    private static IReadOnlyList<int> Widths(IEnumerable<string> keys) =>
        keys.Where(k => k.Length > 1)
            .Select(k => k.Length)
            .Distinct()
            .OrderByDescending(length => length)
            .ToArray();

    private sealed record KeyMaps(
        IReadOnlyDictionary<string, string> ToLayout,
        IReadOnlyDictionary<string, string> ToLatin,
        IReadOnlyList<int> ToLayoutWidths,
        IReadOnlyList<int> ToLatinWidths);
}
