namespace KHost.Mobile.Services;

/// <summary>Builds a KaraFun search URL for a song at a given venue. Pure and host-agnostic so it's trivially testable.</summary>
public static class KaraFunSearch
{
    /// <summary>
    /// Search URL for "Title Artist" under the given venue, e.g. <c>karafun.com/076217/search?q=…</c>. KaraFun's
    /// catalogue search lives under a venue, so the venue ID is part of the path (unlike YouTube/Spotify).
    /// </summary>
    public static string UrlFor(string venueId, string title, string? artist)
    {
        // Space-joined "Title Artist" (like the Spotify builder) — KaraFun's search matches on the combined terms.
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} {artist.Trim()}";
        return $"https://www.karafun.com/{venueId.Trim()}/search?q=" + Uri.EscapeDataString(query);
    }
}
