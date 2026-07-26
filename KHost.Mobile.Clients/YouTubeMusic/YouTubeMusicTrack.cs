namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// One track from a public YouTube Music playlist. A YT Music catalog entry carries title and artist as
/// separate fields, unlike a plain-YouTube video whose title is one blob.
/// </summary>
/// <param name="Title">Song title.</param>
/// <param name="Artist">The artist byline.</param>
/// <param name="VideoId">Links back to the track on YouTube Music.</param>
public sealed record YouTubeMusicTrack(string Title, string Artist, string VideoId);
