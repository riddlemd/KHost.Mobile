namespace KHost.Mobile.Models;

/// <summary>Common music genres offered in the (searchable) genre picker. Free text is still allowed on top of these.</summary>
public static class Genres
{
    public static readonly IReadOnlyList<string> All =
    [
        "Afrobeats",
        "Alternative",
        "Americana",
        "Big Band",
        "Bluegrass",
        "Blues",
        "Christian",
        "Christmas / Holiday",
        "Classic Rock",
        "Classical",
        "Country",
        "Dance",
        "Disco",
        "EDM",
        "Electronic",
        "Emo",
        "Folk",
        "Funk",
        "Gospel",
        "Grunge",
        "Hard Rock",
        "Hip Hop",
        "House",
        "Indie",
        "J-Pop",
        "Jazz",
        "K-Pop",
        "Latin",
        "Metal",
        "Motown",
        "Musical Theatre",
        "New Wave",
        "Oldies",
        "Opera",
        "Pop",
        "Pop Punk",
        "Punk",
        "R&B",
        "Rap",
        "Reggae",
        "Reggaeton",
        "Rock",
        "Salsa",
        "Show Tunes",
        "Singer-Songwriter",
        "Ska",
        "Soul",
        "Soundtrack",
        "Swing",
        "Synthpop",
        "Techno",
        "Trance",
        "World",
    ];

    // Map an external (iTunes) genre onto the app's fixed genre list (the edit field is a <select> of All,
    // so an unmapped value couldn't stick). Exact match, then a few aliases, then a contains fallback.
    // Shared by the detail-sheet auto-fill and the post-import review.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hip-Hop/Rap"] = "Hip Hop",
        ["Singer/Songwriter"] = "Singer-Songwriter",
        ["Holiday"] = "Christmas / Holiday",
        ["Worldwide"] = "World",
        // iTunes reports these hyphenated/compound forms; without an alias the Contains fallback would
        // shadow them onto an earlier, shorter entry ("Pop-Punk"→"Pop", "Reggaeton…"→"Reggae").
        ["Pop-Punk"] = "Pop Punk",
        ["Reggaeton y Hip-Hop"] = "Reggaeton",
    };

    public static string? Map(string? externalGenre)
    {
        if (string.IsNullOrWhiteSpace(externalGenre))
            return null;

        var genre = externalGenre.Trim();

        var exact = All.FirstOrDefault(g => string.Equals(g, genre, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        if (Aliases.TryGetValue(genre, out var alias))
            return alias;

        // e.g. "R&B/Soul" contains "R&B"; "Hip-Hop/Rap" contains "Rap".
        return All.FirstOrDefault(g => genre.Contains(g, StringComparison.OrdinalIgnoreCase));
    }
}
