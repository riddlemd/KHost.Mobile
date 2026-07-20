namespace KHost.Mobile.Clients.YouTubeMusic;

/// <inheritdoc />
/// <remarks>
/// Backend: the public playlist page (<c>music.youtube.com/playlist?list=…</c>), whose initial data is parsed
/// out of the returned HTML. It serves a stripped page to non-browser clients, so every request presents a
/// normal browser User-Agent (set per-request, so the injected <see cref="HttpClient"/> needs no special
/// configuration). No API key — read-only scrape of a public page.
/// </remarks>
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
            // A genuine caller cancellation rethrows as OperationCanceledException; only a real network failure
            // (or a request-timeout TaskCanceledException) maps to the domain error. Mirrors the sibling clients.
            cancellationToken.ThrowIfCancellationRequested();
            throw new YouTubeMusicImportException("Couldn't reach YouTube Music. Check your connection and try again.", ex);
        }

        // Dispose on every exit (including the throw path) so the pooled connection is released.
        using var _ = response;

        // A private/invalid playlist still returns 200 with an error state in the page — the parser's
        // "no tracks" check handles that; a non-200 here is a genuine transport/service problem.
        if (!response.IsSuccessStatusCode)
            throw new YouTubeMusicImportException($"YouTube Music returned an error ({(int)response.StatusCode}). Try again later.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return YouTubeMusicPlaylistParser.Parse(html);
    }
}
