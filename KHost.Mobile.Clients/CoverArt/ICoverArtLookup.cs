namespace KHost.Mobile.Clients.CoverArt;

/// <summary>
/// Finds a cover-art image URL for a song by title + artist. Keyless. A FALLBACK behind the primary metadata
/// lookup, consulted only when that returned no cover. Art ONLY — year and genre stay with the primary lookup
/// even when this answers.
/// </summary>
public interface ICoverArtLookup
{
    /// <summary>
    /// Returns an absolute cover-art URL for <paramref name="title"/> by <paramref name="artist"/>, or null
    /// when nothing matched (or either argument is blank). Throws <see cref="CoverArtLookupException"/> only
    /// on a network/HTTP failure — a "no match" is a null, not an exception.
    /// </summary>
    Task<string?> FindCoverArtUrlAsync(string title, string artist, CancellationToken cancellationToken = default);
}
