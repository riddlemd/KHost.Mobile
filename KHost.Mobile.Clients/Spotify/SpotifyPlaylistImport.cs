namespace KHost.Mobile.Clients.Spotify;

/// <summary>Result of reading a public playlist via the token-free embed endpoint.</summary>
/// <param name="Name">The playlist's name, if the page exposed it; otherwise null.</param>
/// <param name="Tracks">Tracks in playlist order.</param>
/// <param name="LikelyTruncated">The embed exposes at most ~100 tracks and offers no
/// pagination without a token. True when we hit that ceiling, so the caller can warn that a
/// longer playlist was only partially imported. We can't know the true total without a token.</param>
public sealed record SpotifyPlaylistImport(
    string? Name,
    IReadOnlyList<SpotifyTrack> Tracks,
    bool LikelyTruncated);
