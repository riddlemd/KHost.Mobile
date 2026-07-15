namespace KHost.Mobile.Clients.Deezer;

/// <inheritdoc />
/// <remarks>
/// Uses the keyless Deezer public API (<c>api.deezer.com/search</c>). Field-scoped query (<c>artist:"…"
/// track:"…"</c>) so Deezer narrows to the song rather than ranking the artist's whole catalog — which is
/// exactly why it finds covers iTunes' popularity-ranked search misses. Rate limit is ~50 req/5s per IP;
/// callers use this on-demand behind iTunes, so that's ample. Cover art only — release dates from this API
/// are the digital-availability date, not the original release, so year/genre stay with iTunes.
/// </remarks>
public sealed class DeezerCoverArtLookup(HttpClient httpClient) : ICoverArtLookup
{
    public async Task<string?> FindCoverArtUrlAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            return null;

        // Field-scoped advanced query; the quotes keep multi-word titles/artists intact.
        var query = $"artist:\"{artist.Trim()}\" track:\"{title.Trim()}\"";
        var url = $"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit=10";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new DeezerCoverArtException("Couldn't reach Deezer for cover art.", ex);
        }

        if ((int)response.StatusCode == 429)
            throw new DeezerCoverArtException("Deezer cover-art lookups are rate-limited right now.");

        if (!response.IsSuccessStatusCode)
            throw new DeezerCoverArtException($"Deezer error ({(int)response.StatusCode}).");

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return DeezerCoverArtParser.ParseCoverArtUrl(json, title, artist);
    }
}
