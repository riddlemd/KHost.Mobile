namespace KHost.Mobile.Clients.Lyrics;

/// <summary>
/// Lyrics looked up for a song by title + artist. <see cref="PlainLyrics"/> is the display text (null or
/// empty when the source carried none); <see cref="Instrumental"/> flags a track the source marked as having
/// no lyrics. <see cref="SyncedLyrics"/> (LRC) is carried for a possible future synced view but unused today.
/// <see cref="MatchedTitle"/>/<see cref="MatchedArtist"/> are what the source actually matched.
/// </summary>
public sealed record LyricsResult(
    string? MatchedTitle,
    string? MatchedArtist,
    string? PlainLyrics,
    string? SyncedLyrics,
    bool Instrumental);
