namespace KHost.Mobile.Services;

/// <summary>Builds a YouTube search URL for a song. Pure and host-agnostic so it's trivially testable.</summary>
public static class YouTubeSearch
{
    /// <summary>Search-results URL for "Title - Artist" (or just the title when there's no artist).</summary>
    public static string UrlFor(string title, string? artist)
    {
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} - {artist.Trim()}";
        return "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query);
    }
}
