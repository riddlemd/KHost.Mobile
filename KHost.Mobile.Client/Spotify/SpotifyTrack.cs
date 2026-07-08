namespace KHost.Mobile.Client.Spotify;

/// <summary>
/// One track read from a public Spotify playlist. Title + artist are all a karaoke singer
/// needs; <see cref="SpotifyTrackId"/> is kept for a future de-dupe / library-linking step.
/// </summary>
public sealed record SpotifyTrack(string Title, string Artist, string? SpotifyTrackId);
