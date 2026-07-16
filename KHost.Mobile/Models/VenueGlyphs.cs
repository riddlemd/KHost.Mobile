namespace KHost.Mobile.Models;

/// <summary>
/// The curated set of venue icons the add/edit picker offers. A fixed grid beats the native OS emoji keyboard here:
/// in the Blazor WebView that keyboard's emoji section is unreliable, and a hand-picked set stays on-theme
/// (instruments, nightlife, drinks, and tropical/latin locale cues). Stored on <see cref="Venue.Glyph"/> as the raw
/// string; a venue whose saved glyph isn't in this list still renders fine — the picker just won't highlight it.
/// </summary>
public static class VenueGlyphs
{
    /// <summary>The default venue icon (a mic), used for a new venue and as the fallback when none is chosen.</summary>
    public const string Default = "🎤";

    /// <summary>The pickable glyphs, in display order. <see cref="Default"/> leads.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        // Music & instruments
        "🎤", "🎶", "🎸", "🎹", "🥁", "🎷", "🎺", "🎻", "🪗", "🪘", "🎧", "📻",
        // Nightlife & vibe
        "🎭", "🕺", "💃", "🔥", "⭐", "🎉", "🌃", "🎊",
        // Drinks & bar
        "🍸", "🍺", "🍷", "🍹",
        // Locale — tropical / latin
        "🌴", "🏝️", "⛱️", "🌵", "🌮", "🌶️",
    ];
}
