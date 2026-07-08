namespace KHost.Mobile.Client.YouTubeMusic;

/// <summary>Result of reading a public YouTube Music playlist via the token-free page scrape.</summary>
/// <param name="Name">The playlist's name (from the page title), if found; otherwise null.</param>
/// <param name="Tracks">Tracks in playlist order — the first ~100 the page renders inline.</param>
/// <param name="LikelyTruncated">True when the page carried a continuation token, i.e. the playlist has
/// more tracks than the initial payload. Fetching the rest needs YouTube's internal continuation API.</param>
public sealed record YouTubeMusicPlaylistImport(
    string? Name,
    IReadOnlyList<YouTubeMusicTrack> Tracks,
    bool LikelyTruncated);
