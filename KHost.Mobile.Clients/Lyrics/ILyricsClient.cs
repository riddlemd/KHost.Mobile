namespace KHost.Mobile.Clients.Lyrics;

/// <summary>
/// Looks up a song's lyrics by title + artist. Keyless; backed by the LRCLIB API (lrclib.net).
/// </summary>
public interface ILyricsClient
{
    /// <summary>
    /// Looks up lyrics for <paramref name="title"/> by <paramref name="artist"/>. Returns null when
    /// nothing matched (or the title is blank). Throws <see cref="LyricsLookupException"/> only on a
    /// network/HTTP failure.
    /// </summary>
    Task<LyricsResult?> FetchAsync(string title, string artist, CancellationToken cancellationToken = default);
}
