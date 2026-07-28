using System.Net;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Apple;

/// <inheritdoc />
/// <remarks>
/// Uses the keyless iTunes Search API (<c>itunes.apple.com/search</c>). It's fuzzy free-text matching,
/// so callers should sanity-check <see cref="TrackMetadata.MatchedArtist"/> before trusting the result,
/// and it's rate-limited (~20 req/min) — enrich on demand, not in a big parallel burst.
/// </remarks>
internal sealed class ITunesTrackMetadataLookup(HttpClient httpClient, ILookupOptions? options = null) : ITrackMetadataLookup
{
    private readonly ILookupOptions _options = options ?? new DefaultLookupOptions();

    public async Task<TrackLookupResult> LookupAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return TrackLookupResult.None;

        var term = string.IsNullOrWhiteSpace(artist)
            ? SearchText(title)
            : $"{SearchText(artist)} {SearchText(title)}";
        // limit=25, not the top hit: iTunes ranks by popularity, so a cover often outranks the real recording.
        // country decides which catalogue answers at all — a local-language artist is simply absent from another.
        var region = string.IsNullOrWhiteSpace(_options.CatalogueRegion) ? "US" : _options.CatalogueRegion;
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&entity=song&limit=25"
                  + $"&country={Uri.EscapeDataString(region)}";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Check first: a caller cancel must stay an OperationCanceledException, not become a domain error.
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

    // Search on normalized text: iTunes' typo tolerance breaks down once punctuation is in the term —
    // "All Time Low Dear Maria, Count Me Inn" comes back empty, the same query without the comma finds it.
    // Safe because the parser re-verifies every candidate against the raw title/artist.
    private static string SearchText(string value)
        => TrackTextNormalizer.Normalize(value) is { Length: > 0 } normalized ? normalized : value.Trim();
}
