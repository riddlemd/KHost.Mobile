using System.Net;

namespace KHost.Mobile.Client.Spotify;

/// <inheritdoc />
public sealed class SpotifyImportService(HttpClient httpClient) : ISpotifyImportService
{
    private const string EmbedUrlFormat = "https://open.spotify.com/embed/playlist/{0}";

    // The embed renders its tracklist server-side, but is served a stripped page to non-browser
    // clients — so we present a normal browser User-Agent. Set per-request so the service works
    // regardless of how the injected HttpClient was configured.
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
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SpotifyImportException("Couldn't reach Spotify. Check your connection and try again.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new SpotifyImportException("That playlist couldn't be found — make sure it's public.");

        if (!response.IsSuccessStatusCode)
            throw new SpotifyImportException($"Spotify returned an error ({(int)response.StatusCode}). Try again later.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return SpotifyEmbedParser.Parse(html);
    }
}
