using KHost.Mobile.Abstractions.Clients.Matching;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Deezer;

/// <inheritdoc />
/// <remarks>
/// Uses the keyless Deezer public API (<c>api.deezer.com/search</c>), and deliberately NOT the field-scoped
/// <c>artist:"…" track:"…"</c> form <see cref="DeezerCoverArtLookup"/> uses. Measured against the live API,
/// the field-scoped query is exact-only — a typo returns zero results — while the plain free-text query is
/// typo-tolerant ("Radiohead Creap" → "Creep — Radiohead"). Hence a separate call rather than a second
/// reading of the cover-art response.
/// <para>Rate limit ~50 req/5s per IP; reached only for a song iTunes couldn't place, once per song.</para>
/// </remarks>
internal sealed class DeezerSpellingSuggestionLookup(HttpClient httpClient) : ISpellingSuggestionLookup
{
    public async Task<TrackSuggestion?> SuggestAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            return null;

        var url = $"https://api.deezer.com/search?q={Uri.EscapeDataString($"{artist.Trim()} {title.Trim()}")}&limit=10";

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return DeezerSuggestionParser.ParseSuggestion(json, title, artist);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A caller cancellation still propagates; anything else is a hint we simply don't get this time.
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
