namespace KeyboardLanguageFix.Core;

/// <summary>Access to the built-in layout tables. The data lives in Layouts.g.cs.</summary>
public static partial class Layouts
{
    private static readonly IReadOnlyList<Layout> AllLayouts = Build();

    private static readonly IReadOnlyDictionary<string, Layout> ById =
        AllLayouts.ToDictionary(layout => layout.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every layout the app knows about, in display order.</summary>
    public static IReadOnlyList<Layout> All => AllLayouts;

    /// <summary>The layout with this id, or <c>null</c> when there is none.</summary>
    public static Layout? Find(string? id) =>
        id is not null && ById.TryGetValue(id, out var layout) ? layout : null;

    /// <summary>The layout with this id, falling back to Arabic.</summary>
    public static Layout Get(string? id) => Find(id) ?? ById["ar"];
}
