using System.Net;

namespace KHost.Mobile.Clients.Lyrics;

/// <inheritdoc />
/// <remarks>
/// Uses the keyless LRCLIB search API (<c>lrclib.net/api/search</c>). LRCLIB's fair-use policy asks for a
/// descriptive <c>User-Agent</c>; that (and the base address) are configured on the injected
/// <see cref="HttpClient"/> at registration. Free-text search on "{artist} {title}" + first usable result,
/// matching KHost's lookup.
/// </remarks>
public sealed class LrcLibLyricsClient(HttpClient httpClient) : ILyricsClient
{
    public async Task<LyricsResult?> FetchAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var query = string.IsNullOrWhiteSpace(artist) ? title.Trim() : $"{artist.Trim()} {title.Trim()}";
        var url = $"api/search?q={Uri.EscapeDataString(query)}";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A genuine caller-cancel bubbles as OperationCanceledException; only a real failure is wrapped.
            cancellationToken.ThrowIfCancellationRequested();
            throw new LyricsLookupException("Couldn't reach the lyrics service. Check your connection and try again.", ex);
        }

        // Dispose on every exit (the 404 fast path is the COMMON outcome) so the pooled connection is released.
        using var _ = response;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;   // no match — not an error

        if ((int)response.StatusCode == 429)
            throw new LyricsLookupException("Lyrics lookups are rate-limited right now — wait a moment and try again.");

        if (!response.IsSuccessStatusCode)
            throw new LyricsLookupException($"Lyrics service error ({(int)response.StatusCode}). Try again later.");

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return LrcLibResponseParser.ParseFirst(json);
    }
}
