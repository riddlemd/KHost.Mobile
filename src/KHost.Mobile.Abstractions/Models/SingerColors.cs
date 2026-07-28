namespace KHost.Mobile.Abstractions.Models;

/// <summary>
/// The curated set of accent colors a singer can carry; the active singer's choice re-tints the app. A fixed
/// palette rather than a free color wheel, so every choice stays legible on both the light and dark grounds.
/// Stored on <see cref="Singer.Color"/> as a raw hex string; a saved color that isn't in this list still renders
/// fine — the picker just won't highlight it.
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
