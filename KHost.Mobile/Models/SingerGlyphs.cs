namespace KHost.Mobile.Models;

/// <summary>
/// The curated set of emoji a singer can pick for their avatar instead of the name's first letter. A fixed grid
/// rather than the native OS emoji keyboard, whose emoji section is unreliable in the Blazor WebView. Stored on
/// <see cref="Singer.Glyph"/> as the raw string; a saved glyph that isn't in this list still renders fine — the
/// picker just won't highlight it. A blank glyph means "use the first letter".
/// </summary>
public static class SingerGlyphs
{
    /// <summary>The pickable glyphs, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        // Music & nightlife
        "🎤", "🎸", "🎧", "🎹", "🥁", "🎷", "🕺", "💃", "🎭", "⭐", "🔥", "👑",
        // Faces & fun
        "😎", "🤩", "😊", "🥳", "🤠", "🙂",
        // Animals
        "🦄", "🐱", "🐶", "🐼", "🦊", "🐸", "🐵", "🦁", "🐯", "🐨",
    ];
}
