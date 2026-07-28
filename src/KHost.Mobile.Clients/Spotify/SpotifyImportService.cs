using System.Net;

using KHost.Mobile.Abstractions.Clients.Spotify;

namespace KHost.Mobile.Clients.Spotify;

/// <inheritdoc />
/// <remarks>
/// Backend: the public embed page (<c>open.spotify.com/embed/playlist/…</c>), which renders its tracklist
/// server-side. It serves a stripped page to non-browser clients, so every request presents a normal browser
/// User-Agent (set per-request, so the injected <see cref="HttpClient"/> needs no special configuration).
/// No API key — read-only scrape of a public page.
/// </remarks>
internal sealed class SpotifyImportService(HttpClient httpClient) : ISpotifyImportService
{
    private const string EmbedUrlFormat = "https://open.spotify.com/embed/playlist/{0}";

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    public async Task<SpotifyPlaylistImport> FetchPlaylistAsync(string playlistUrlOrId, CancellationToken cancellationToken = default)
    {
        if (!SpotifyPlaylistUrl.TryParseId(playlistUrlOrId, out var id))
            throw new SpotifyImportException("That doesn't look like a Spotify playlist link.");

        using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(EmbedUrlFormat, id));
        request.Headers.UserAgent.ParseAdd(BrowserUserAgent);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Check first: a caller cancel must stay an OperationCanceledException, not become a domain error.
            cancellationToken.ThrowIfCancellationRequested();
            throw new SpotifyImportException("Couldn't reach Spotify. Check your connection and try again.", ex);
        }

        // Dispose on every exit (including the throw paths) so the pooled connection is released.
        using var _ = response;

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new SpotifyImportException("That playlist couldn't be found — make sure it's public.");

        if (!response.IsSuccessStatusCode)
            throw new SpotifyImportException($"Spotify returned an error ({(int)response.StatusCode}). Try again later.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return SpotifyEmbedParser.Parse(html);
    }
}
