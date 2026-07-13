namespace KHost.Mobile.Clients.Enrichment;

/// <summary>
/// Looks up release year + genre for a song by title + artist. Keyless; backed by the iTunes Search API.
/// </summary>
public interface ITrackMetadataLookup
{
    /// <summary>
    /// Looks up metadata for <paramref name="title"/> by <paramref name="artist"/>. Returns null when
    /// nothing matched (or the title is blank). Throws <see cref="MetadataLookupException"/> only on a
    /// network/HTTP failure.
    /// </summary>
    Task<TrackMetadata?> LookupAsync(string title, string artist, CancellationToken cancellationToken = default);
}
