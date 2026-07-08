using KHost.Mobile.Client.Enrichment;

namespace KHost.Mobile.Services;

/// <summary>
/// Looks up a release-year + genre suggestion for a song, caching each unique title+artist so the
/// underlying lookup service is called at most once per song (cached "no match" included). The cache
/// persists across restarts, which keeps us comfortably under the lookup API's rate limit.
/// </summary>
public interface IMetadataSuggester
{
    /// <summary>
    /// Returns a cached-or-fetched suggestion, or null when nothing matched. A transient network/rate-limit
    /// failure also returns null and is NOT cached, so a later attempt can retry.
    /// </summary>
    Task<TrackMetadata?> SuggestAsync(string title, string artist, CancellationToken cancellationToken = default);
}
