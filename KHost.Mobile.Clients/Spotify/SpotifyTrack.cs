namespace KHost.Mobile.Clients.Spotify;

/// <summary>One track read from a public Spotify playlist.</summary>
/// <param name="Title">Song title.</param>
/// <param name="Artist">Performing artist.</param>
/// <param name="SpotifyTrackId">Kept for a future de-dupe / library-linking step.</param>
public sealed record SpotifyTrack(string Title, string Artist, string? SpotifyTrackId);
