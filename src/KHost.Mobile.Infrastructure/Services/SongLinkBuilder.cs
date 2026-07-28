using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>Pure and host-agnostic — every method is string building, so it's trivially testable.</remarks>
// logger optional so a test can `new` it; DI supplies the real one.
internal sealed class SongLinkBuilder(ILogger<SongLinkBuilder>? logger = null) : ISongLinkBuilder
{
    // KaraFun's venue search expects the q value to carry an "sc_" search-context prefix; without it the page
    // loads but returns no matches. Kept as a constant since this token may be reworked upstream.
    private const string KaraFunQueryPrefix = "sc_";

    private readonly ILogger _log = logger ?? NullLogger<SongLinkBuilder>.Instance;

    /// <inheritdoc />
    public string YouTubeUrlFor(string title, string? artist)
    {
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} - {artist.Trim()}";
        return "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query);
    }

    /// <inheritdoc />
    public string SpotifyUrlFor(string title, string? artist)
    {
        // Space-separated (no "- ") because Spotify search treats a leading dash as a NOT operator, which would
        // exclude the artist instead of searching for it.
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} {artist.Trim()}";
        return "https://open.spotify.com/search/" + Uri.EscapeDataString(query);
    }

    /// <inheritdoc />
    public string KaraFunUrlFor(string venueId, string title, string? artist)
    {
        // Space-joined "Title Artist" (like the Spotify builder) — KaraFun's search matches on the combined terms.
        var query = string.IsNullOrWhiteSpace(artist)
            ? title.Trim()
            : $"{title.Trim()} {artist.Trim()}";
        return $"https://www.karafun.com/{venueId.Trim()}/search?q={KaraFunQueryPrefix}" + Uri.EscapeDataString(query);
    }

    /// <inheritdoc />
    public string KaraFunCatalogUrlFor(string venueId) => $"https://www.karafun.com/{venueId.Trim()}/";
}
