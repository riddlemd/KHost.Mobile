namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// Reads a public YouTube Music playlist's tracks with no token or login, via the
/// <c>music.youtube.com/playlist?list={id}</c> page. Returns clean title + artist (see
/// <see cref="YouTubeMusicTrack"/>) and is capped at ~100 tracks by the initial page payload.
/// </summary>
public interface IYouTubeMusicImportService
{
    /// <summary>
    /// Fetches and parses a public playlist. <paramref name="playlistUrlOrId"/> may be a music.youtube.com
    /// or youtube.com URL, a browse link, or a bare id. Throws <see cref="YouTubeMusicImportException"/>
    /// on a bad link, network failure, HTTP error, or an unrecognized page.
    /// </summary>
    Task<YouTubeMusicPlaylistImport> FetchPlaylistAsync(string playlistUrlOrId, CancellationToken cancellationToken = default);
}
