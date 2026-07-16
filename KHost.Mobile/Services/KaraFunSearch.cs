namespace KHost.Mobile.Services;

/// <summary>Builds a KaraFun search URL for a song at a given venue. Pure and host-agnostic so it's trivially testable.</summary>
public static class KaraFunSearch
{
    // KaraFun's venue search expects the q value to carry an "sc_" search-context prefix; without it the page
    // loads but returns no matches. Kept as a constant since this token may be reworked upstream.
    private const string QueryPrefix = "sc_";

    /// <summary>
    /// Search URL for "Title Artist" under the given venue, e.g. <c>karafun.com/012345/search?q=sc_…</c>. KaraFun's
    /// catalogue search lives under a venue, so the venue ID is part of the path (unlike YouTube/Spotify).
    /// </summary>
    public static string UrlFor(string venueId, string title, string? artist)
    {
        // Space-joined "Title Artist" (like the Spotify builder) — KaraFun's search matches on the combined terms.
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} {artist.Trim()}";
        return $"https://www.karafun.com/{venueId.Trim()}/search?q={QueryPrefix}" + Uri.EscapeDataString(query);
    }

    /// <summary>
    /// The venue's KaraFun catalog home (no song query), e.g. <c>karafun.com/012345/</c> — for "Open KaraFun
    /// Catalog" on the venue page, distinct from the song-scoped <see cref="UrlFor"/>. Opens the venue's whole
    /// songbook so the singer can browse it directly.
    /// </summary>
    public static string CatalogUrlFor(string venueId) => $"https://www.karafun.com/{venueId.Trim()}/";
}
