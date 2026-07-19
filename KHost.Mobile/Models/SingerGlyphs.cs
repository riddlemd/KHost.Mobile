namespace KHost.Mobile.Models;

/// <summary>
/// The curated set of emoji a singer can pick for their avatar instead of the name's first letter. A fixed grid
/// beats the native OS emoji keyboard here (unreliable in the Blazor WebView) and keeps the choices on-theme —
/// faces, animals, and nightlife/music cues that read as a personal marker. Stored on <see cref="Singer.Glyph"/>
/// as the raw string; a singer whose saved glyph isn't in this list still renders fine — the picker just won't
/// highlight it. A blank glyph means "use the first letter", offered as a separate option in the picker.
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
