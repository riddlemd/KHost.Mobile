namespace KHost.Mobile.Models;

/// <summary>
/// The curated set of accent colors a singer can carry. Each singer picks one; the whole app chrome (header
/// gradient, active tab, primary buttons) recolors to the active singer's choice, so a glance tells you whose
/// phone this is. A fixed palette — rather than a free color wheel — keeps every choice legible on both the light
/// and dark grounds and on-theme with the violet brand. Stored on <see cref="Singer.Color"/> as a raw hex string;
/// a singer whose saved color isn't in this list still renders fine — the picker just won't highlight it.
/// </summary>
public static class SingerColors
{
    /// <summary>The default accent (the brand violet), used for a new singer and as the fallback when none is set.</summary>
    public const string Default = "#7c3aed";

    /// <summary>The pickable colors, in display order. <see cref="Default"/> leads.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "#7c3aed", // violet (brand)
        "#0d9488", // teal
        "#d97706", // amber
        "#e11d48", // rose
        "#2563eb", // blue
        "#16a34a", // green
        "#db2777", // pink
        "#64748b", // slate
    ];
}
