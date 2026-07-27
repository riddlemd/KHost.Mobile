namespace KHost.Mobile.Models;

/// <summary>
/// The curated set of venue icons the picker offers. A fixed grid rather than the native OS emoji keyboard, whose
/// emoji section is unreliable in the Blazor WebView. Stored on <see cref="Venue.Glyph"/> as the raw string; a
/// saved glyph that isn't in this list still renders fine — the picker just won't highlight it.
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
