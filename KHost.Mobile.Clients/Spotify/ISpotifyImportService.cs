namespace KHost.Mobile.Clients.Spotify;

/// <summary>
/// Reads a public Spotify playlist's tracks with no token or login, via the
/// <c>open.spotify.com/embed/playlist/{id}</c> endpoint. Returns names + artists only
/// (see <see cref="SpotifyTrack"/>) and is capped at ~100 tracks by the embed.
/// </summary>
public interface ISpotifyImportService
{
    /// <summary>
    /// Fetches and parses a public playlist. <paramref name="playlistUrlOrId"/> may be a web
    /// URL, a <c>spotify:</c> URI, or a bare id. Throws <see cref="SpotifyImportException"/>
    /// on a bad link, network failure, HTTP error, or an unrecognized page.
    /// </summary>
    Task<SpotifyPlaylistImport> FetchPlaylistAsync(string playlistUrlOrId, CancellationToken cancellationToken = default);
}
