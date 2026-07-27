namespace KHost.Mobile.Clients.Lyrics;

/// <summary>Lyrics looked up for a song by title + artist.</summary>
/// <param name="MatchedTitle">What the source actually matched.</param>
/// <param name="MatchedArtist">What the source actually matched.</param>
/// <param name="PlainLyrics">Display text; null or empty when the source carried none.</param>
/// <param name="SyncedLyrics">Time-synced LRC — carried for a possible future synced view, unused today.</param>
/// <param name="Instrumental">True when the source marked the track as having no lyrics.</param>
public sealed record LyricsResult(
    string? MatchedTitle,
    string? MatchedArtist,
    string? PlainLyrics,
    string? SyncedLyrics,
    bool Instrumental);
