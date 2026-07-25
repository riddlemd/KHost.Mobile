using KHost.Mobile.Clients.Deezer;
using KHost.Mobile.Clients.Enrichment;
using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// <para>Covers are streamed to the WebView and turned into <c>blob:</c> URLs by <c>wwwroot/js/album-art.js</c> —
/// see DEVELOPMENT.md for why that hop exists instead of serving the cached files directly.</para>
/// <para>Work is drained one song at a time from a queue rather than run in parallel. That keeps the single-writer
/// guarantee the old loader had (two overlapping passes could have one revoke a blob the other just installed,
/// leaving a card pointing at a dead URL), and the drain runs on the UI context so its JS interop stays valid.</para>
/// </remarks>
public sealed class AlbumArtService(
    IJSRuntime js,
    IAlbumArtCache cache,
    IAppSettings settings,
    ISongListStore store,
    ITrackMetadataLookup metadata,
    ICoverArtLookup artFallback,
    ILogger<AlbumArtService>? logger = null) : IAlbumArtService
{
    private readonly ILogger<AlbumArtService> _log =
        logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AlbumArtService>.Instance;

    // NO SIZE CAP, deliberately — don't add one without reading this.
    //
    // An LRU cap was tried and thrashes. My Songs keeps every card it has scrolled past in the DOM and asks for
    // all of their covers on every render, so a cap below the number of rendered cards evicts a cover that the
    // very next render asks for again: measured on-device as an endless fetch/evict loop that never settled.
    // Capping safely needs to know which cards are actually in the viewport, which this service cannot see and
    // the renderer doesn't track. So covers are held until something explicitly drops them — a singer switch, an
    // edit, or clearing the cache — which is what the previous design did in practice anyway.
    /// <summary>
    /// Pause after each lookup that actually went to the network. Scrolling a long list now asks about every
    /// song it renders, and most libraries are mostly coverless (~80%), so without this a scroll fires hundreds
    /// of back-to-back iTunes calls and earns a rate-limit block. Downloads of an already-known cover URL hit the
    /// artwork CDN instead and aren't paced.
    /// </summary>
    private static readonly TimeSpan DiscoveryPause = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<Guid, string> _uris = [];
    private readonly HashSet<Guid> _queued = [];     // queued or already attempted this session
    private readonly HashSet<Guid> _fetching = [];   // still to finish — drives the loading placeholder
    private readonly Queue<SongListItem> _pending = new();
    private bool _draining;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public string? UriFor(SongListItem song)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (!settings.AlbumArtEnabled)
            return null;

        if (_uris.TryGetValue(song.Id, out var uri))
            return uri;

        // Nothing to chase if the song has no cover and we've already been told so.
        var findable = song.ArtworkUrl is not null || !song.ArtworkLookedUp;
        if (findable && _queued.Add(song.Id))
        {
            _fetching.Add(song.Id);
            _pending.Enqueue(song);
            StartDraining();
        }
        return null;
    }

    /// <inheritdoc />
    public bool IsFetching(SongListItem song)
    {
        ArgumentNullException.ThrowIfNull(song);
        // ArtworkUrl is the "we know there's an image coming" part: before discovery runs it's null, and
        // promising a cover we might not find would flash a placeholder across most of the list.
        return settings.AlbumArtEnabled
            && song.ArtworkUrl is not null
            && _fetching.Contains(song.Id)
            && !_uris.ContainsKey(song.Id);
    }

    /// <inheritdoc />
    public async Task DropAsync(Guid songId)
    {
        if (_uris.Remove(songId))
            await RevokeAsync(songId);
        _queued.Remove(songId);        // let the next request re-fetch it
        _fetching.Remove(songId);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        _uris.Clear();
        _queued.Clear();
        _fetching.Clear();
        _pending.Clear();
        try { await js.InvokeVoidAsync("khAlbumArt.revokeAll"); }
        catch (JSException ex) { _log.LogWarning(ex, "Revoking album-art blob URLs failed"); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Fire-and-forget on the UI context: the drain awaits, so its continuations (and their JS interop) come back
    // to the same context. Only one drain runs at a time; a request arriving mid-drain just joins the queue.
    private void StartDraining()
    {
        if (_draining)
            return;
        _draining = true;
        _ = DrainAsync();
    }

    private async Task DrainAsync()
    {
        try
        {
            while (_pending.Count > 0)
            {
                var song = _pending.Dequeue();
                try
                {
                    await PopulateAsync(song);
                }
                catch (Exception ex)
                {
                    // One song's cover failing must never stop the rest of the queue.
                    _log.LogWarning(ex, "Album art failed for “{Title}” — “{Artist}”", song.Title, song.Artist);
                }
                finally
                {
                    // However it ended — cover, no cover, or a failure — this song is no longer on its way in, so
                    // the placeholder must come down.
                    if (_fetching.Remove(song.Id))
                        Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        finally
        {
            _draining = false;
        }
    }

    private async Task PopulateAsync(SongListItem song)
    {
        if (!settings.AlbumArtEnabled)
            return;

        // Discovering where the cover lives is part of fetching it. Doing this here is the point of the service:
        // it used to live on My Songs, so any other surface showing an unvisited song got a blank card.
        if (song.ArtworkUrl is null)
        {
            if (song.ArtworkLookedUp)
                return;
            await DiscoverArtworkUrlAsync(song);
            await Task.Delay(DiscoveryPause);
            if (song.ArtworkUrl is null)
                return;
            // Discovery just proved there IS a cover coming, which is what IsFetching keys off — repaint so the
            // placeholder appears for the download that follows.
            Changed?.Invoke(this, EventArgs.Empty);
        }

        var stream = await cache.OpenArtStreamAsync(song.ArtworkUrl);
        if (stream is null)
            return;

        string objectUrl;
        try
        {
            // Streamed rather than passed as a byte[], which would base64 in transit. DotNetStreamReference
            // disposes the stream once the WebView has read it.
            objectUrl = await js.InvokeAsync<string>("khAlbumArt.set", song.Id.ToString(), new DotNetStreamReference(stream));
        }
        catch (JSException)
        {
            stream.Dispose();
            throw;
        }

        _uris[song.Id] = objectUrl;
        _log.LogDebug("Album art ready for “{Title}” — “{Artist}”", song.Title, song.Artist);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // iTunes is primary; Deezer is consulted only when iTunes carries no cover, because its popularity-ranked
    // search misses album deep cuts. Either way the attempt is recorded so a coverless song is never re-chased.
    private async Task DiscoverArtworkUrlAsync(SongListItem song)
    {
        if (string.IsNullOrWhiteSpace(song.Title) || string.IsNullOrWhiteSpace(song.Artist))
            return;

        _log.LogDebug("Artwork lookup start: “{Title}” — “{Artist}”", song.Title, song.Artist);
        string? art;
        try
        {
            var meta = await metadata.LookupAsync(song.Title, song.Artist);
            if (meta?.ArtworkUrl is string itunesArt)
            {
                art = itunesArt;
                _log.LogDebug("Artwork from iTunes for “{Title}” — “{Artist}”", song.Title, song.Artist);
            }
            else
            {
                art = await artFallback.FindCoverArtUrlAsync(song.Title, song.Artist);
                _log.LogDebug("iTunes had no cover for “{Title}” — “{Artist}”; Deezer fallback → {Result}",
                    song.Title, song.Artist, art is null ? "no cover found" : "cover found");
            }
        }
        catch (Exception ex) when (ex is MetadataLookupException or DeezerCoverArtException)
        {
            // Network / rate-limit failure on either source: leave ArtworkLookedUp unset so a later session
            // retries, and let this song out of the queue so the same request can try again.
            _log.LogWarning(ex, "Artwork lookup failed for “{Title}” — “{Artist}”; will retry later", song.Title, song.Artist);
            _queued.Remove(song.Id);
            return;
        }

        if (art is not null)
            song.ArtworkUrl = art;
        song.ArtworkLookedUp = true;   // record the attempt, hit or miss, so we never re-spend on it
        await store.UpdateAsync(song);
    }

    private async Task RevokeAsync(Guid songId)
    {
        try { await js.InvokeVoidAsync("khAlbumArt.revoke", songId.ToString()); }
        catch (JSException ex) { _log.LogWarning(ex, "Revoking the album-art blob URL for {SongId} failed", songId); }
    }
}
