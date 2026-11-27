using KHost.Mobile.Abstractions.Clients.Search;

namespace KHost.Mobile.Clients.Deezer;

/// <inheritdoc />
/// <remarks>
/// The keyless Deezer public API (<c>api.deezer.com/search</c>) with the plain free-text query: measured live, it
/// tolerates typos and unfinished words ("dont stop belie" → Journey) where the field-scoped form is exact-only.
/// Asks for 25 so there are still enough distinct songs after versions fold together. Rate limit ~50 req/5 s per
/// IP; the panel debounces keystrokes, so a typed title costs a handful of calls.
/// </remarks>
internal sealed class DeezerSongSearch(HttpClient httpClient) : ISongSearch
{
    /// <summary>Shorter than this and the results are noise ("a", "lo").</summary>
    internal const int MinimumQueryLength = 2;

    public async Task<IReadOnlyList<SongSearchHit>?> SearchAsync(
        string title, string? artist, int limit = 6, CancellationToken cancellationToken = default)
    {
        var query = $"{artist?.Trim()} {title?.Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < MinimumQueryLength)
            return [];

        var url = $"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit=25";
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return DeezerSongSearchParser.Parse(json, limit);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The caller's own cancellation (a newer keystroke) propagates; a timeout or no network is "no search".
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
