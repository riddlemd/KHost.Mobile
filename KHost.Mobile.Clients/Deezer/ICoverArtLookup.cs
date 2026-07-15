namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Finds a cover-art image URL for a song by title + artist. Keyless. Used as a FALLBACK behind the iTunes
/// lookup: iTunes stays the primary source (and the only source for year/genre — Deezer's release dates are
/// unreliable), and this is consulted only when iTunes returns no cover.
/// </summary>
public interface ICoverArtLookup
{
    /// <summary>
    /// Returns an absolute cover-art URL for <paramref name="title"/> by <paramref name="artist"/>, or null
    /// when nothing matched (or either argument is blank). Throws <see cref="DeezerCoverArtException"/> only
    /// on a network/HTTP failure — a "no match" is a null, not an exception.
    /// </summary>
    Task<string?> FindCoverArtUrlAsync(string title, string artist, CancellationToken cancellationToken = default);
}
