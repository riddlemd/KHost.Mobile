namespace KHost.Mobile.Abstractions.Clients.Spotify;

/// <summary>Result of reading a public playlist via the token-free embed endpoint.</summary>
/// <param name="Name">The playlist's name, if the page exposed it; otherwise null.</param>
/// <param name="Tracks">Tracks in playlist order.</param>
/// <param name="LikelyTruncated">True when the ~100-track embed ceiling was hit. The embed offers no
/// pagination without a token, so the real total is unknowable — hence "likely", not "was".</param>
public sealed record SpotifyPlaylistImport(
    string? Name,
    IReadOnlyList<SpotifyTrack> Tracks,
    bool LikelyTruncated);
