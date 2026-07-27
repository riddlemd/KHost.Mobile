namespace KHost.Mobile.Clients.Metadata;

/// <summary>
/// Looks up release year, genre, and cover-art URL for a song by title + artist. Keyless.
/// </summary>
public interface ITrackMetadataLookup
{
    /// <summary>
    /// Looks up metadata for <paramref name="title"/> by <paramref name="artist"/>. Returns
    /// <see cref="TrackLookupResult.None"/> when nothing matched (or the title is blank), and a
    /// <see cref="TrackLookupResult.Suggestion"/> when the only near-miss looks like a misspelling.
    /// Throws <see cref="MetadataLookupException"/> only on a network/HTTP failure.
    /// </summary>
    Task<TrackLookupResult> LookupAsync(string title, string artist, CancellationToken cancellationToken = default);
}
