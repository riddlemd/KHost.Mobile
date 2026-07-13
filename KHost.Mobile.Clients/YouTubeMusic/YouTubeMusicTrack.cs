namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// One track from a public YouTube Music playlist. Unlike a plain-YouTube video, a YT Music catalog
/// entry carries <see cref="Title"/> and <see cref="Artist"/> as separate fields (the artist byline),
/// so this maps cleanly to the app's song model. <see cref="VideoId"/> links back to the track.
/// </summary>
public sealed record YouTubeMusicTrack(string Title, string Artist, string VideoId);
