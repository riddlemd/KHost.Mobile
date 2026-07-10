namespace KHost.Mobile.Models;

/// <summary>Common music genres offered in the (searchable) genre picker. Free text is still allowed on top of these.</summary>
public static class Genres
{
    public static readonly IReadOnlyList<string> All =
    [
        "Pop",
        "Rock",
        "Classic Rock",
        "Alternative",
        "Indie",
        "Punk",
        "Metal",
        "Grunge",
        "Hip Hop",
        "Rap",
        "R&B",
        "Soul",
        "Funk",
        "Disco",
        "Dance",
        "Electronic",
        "EDM",
        "House",
        "Techno",
        "Country",
        "Folk",
        "Bluegrass",
        "Blues",
        "Jazz",
        "Swing",
        "Big Band",
        "Classical",
        "Opera",
        "Musical Theatre",
        "Show Tunes",
        "Soundtrack",
        "Reggae",
        "Ska",
        "Gospel",
        "Christian",
        "Latin",
        "K-Pop",
        "World",
        "Motown",
        "Singer-Songwriter",
        "Christmas / Holiday",
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
