namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// One track from a public YouTube Music playlist. Unlike a plain-YouTube video, a YT Music catalog entry
/// carries title and artist as separate fields (the artist byline), so this maps cleanly to the song model.
/// </summary>
/// <param name="Title">Song title.</param>
/// <param name="Artist">The artist byline.</param>
/// <param name="VideoId">Links back to the track on YouTube Music.</param>
public sealed record YouTubeMusicTrack(string Title, string Artist, string VideoId);
