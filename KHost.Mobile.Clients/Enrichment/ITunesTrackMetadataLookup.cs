using System.Net;
using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Enrichment;

/// <inheritdoc />
/// <remarks>
/// Uses the keyless iTunes Search API (<c>itunes.apple.com/search</c>). It's fuzzy free-text matching,
/// so callers should sanity-check <see cref="TrackMetadata.MatchedArtist"/> before trusting the result,
/// and it's rate-limited (~20 req/min) — enrich on demand, not in a big parallel burst.
/// </remarks>
public sealed class ITunesTrackMetadataLookup(HttpClient httpClient) : ITrackMetadataLookup
{
    public async Task<TrackLookupResult> LookupAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return TrackLookupResult.None;

        var term = string.IsNullOrWhiteSpace(artist)
            ? SearchText(title)
            : $"{SearchText(artist)} {SearchText(title)}";
        // Pull a handful of candidates (not just the top hit) so the real recording can still be found when
        // iTunes ranks a cover first; ParseBestMatch then keeps only a genuine artist+title match.
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&entity=song&limit=25&country=US";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A genuine caller cancellation rethrows as OperationCanceledException; only a real network failure
            // (or a request-timeout TaskCanceledException) maps to the domain error. Mirrors the sibling lookups.
            cancellationToken.ThrowIfCancellationRequested();
            throw new MetadataLookupException("Couldn't reach the lookup service. Check your connection and try again.", ex);
        }

        // Dispose on every exit (including the throw paths) so the pooled connection is released.
        using var _ = response;

        if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
            throw new MetadataLookupException("Lookups are rate-limited right now — wait a moment and try again.");

        if (!response.IsSuccessStatusCode)
            throw new MetadataLookupException($"Lookup service error ({(int)response.StatusCode}). Try again later.");

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ITunesResponseParser.ParseBestMatch(json, title, artist);
    }

    // Search on the same normalized text the parser verifies against, rather than the raw title/artist.
    // iTunes' typo tolerance is inconsistent once punctuation is in the term — "All Time Low Dear Maria,
    // Count Me Inn" comes back empty while the same query without the comma finds the song. Costs nothing:
    // every candidate is re-checked against the raw text here anyway, so the term is only for retrieval.
    // Falls back to the raw text when normalizing empties it (a title that is nothing but "(…)").
    private static string SearchText(string value)
        => TrackTextNormalizer.Normalize(value) is { Length: > 0 } normalized ? normalized : value.Trim();
}
