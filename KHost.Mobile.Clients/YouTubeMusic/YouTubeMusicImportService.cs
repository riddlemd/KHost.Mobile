namespace KHost.Mobile.Clients.YouTubeMusic;

/// <inheritdoc />
public sealed class YouTubeMusicImportService(HttpClient httpClient) : IYouTubeMusicImportService
{
    private const string PlaylistUrlFormat = "https://music.youtube.com/playlist?list={0}";

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    public async Task<YouTubeMusicPlaylistImport> FetchPlaylistAsync(string playlistUrlOrId, CancellationToken cancellationToken = default)
    {
        if (!YouTubeMusicPlaylistUrl.TryParseId(playlistUrlOrId, out var id))
            throw new YouTubeMusicImportException("That doesn't look like a YouTube Music playlist link.");

        using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(PlaylistUrlFormat, id));
        request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");   // stable English titles

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new YouTubeMusicImportException("Couldn't reach YouTube Music. Check your connection and try again.", ex);
        }

        // A private/invalid playlist still returns 200 with an error state in the page — the parser's
        // "no tracks" check handles that; a non-200 here is a genuine transport/service problem.
        if (!response.IsSuccessStatusCode)
            throw new YouTubeMusicImportException($"YouTube Music returned an error ({(int)response.StatusCode}). Try again later.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return YouTubeMusicPlaylistParser.Parse(html);
    }
}
