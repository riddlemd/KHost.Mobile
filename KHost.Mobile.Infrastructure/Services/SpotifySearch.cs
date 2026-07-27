namespace KHost.Mobile.Services;

/// <summary>Builds a Spotify search URL for a song. Pure and host-agnostic so it's trivially testable.</summary>
public static class SpotifySearch
{
    /// <summary>Search URL for "Title Artist". An <c>open.spotify.com</c> link deep-links into the Spotify app
    /// when it's installed, and opens the web player otherwise.</summary>
    public static string UrlFor(string title, string? artist)
    {
        // Space-separated (no "- ") because Spotify search treats a leading dash as a NOT operator, which would
        // exclude the artist instead of searching for it.
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} {artist.Trim()}";
        return "https://open.spotify.com/search/" + Uri.EscapeDataString(query);
    }
}
