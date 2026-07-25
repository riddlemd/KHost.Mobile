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
/// <para>The queue drains one song at a time, on the UI context: overlapping passes can revoke a blob the other
/// just installed, and the JS interop needs the Blazor sync context.</para>
/// </remarks>
public sealed class AlbumArtService(
    IJSRuntime js,
    IAlbumArtCache cache,
    IAppSettings settings,
    ISongListStore store,
    ITrackMetadataLookup metadata,
    ICoverArtLookup artFallback,
    ILogger<AlbumArtService>? logger = null) : IAlbumArtService, IDisposable
{
    private DotNetObjectReference<AlbumArtService>? _selfRef;

    private readonly ILogger<AlbumArtService> _log =
        logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AlbumArtService>.Instance;

    /// <summary>
    /// Pause after each lookup that hits the network — an unpaced sweep over a mostly-coverless library fires
    /// enough back-to-back iTunes calls to get rate-limited. Cover downloads (CDN) aren't paced.
    /// </summary>
    private static readonly TimeSpan DiscoveryPause = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Off-screen covers held as headroom for scrolling back before the longest-gone are dropped. Never limits
    /// what's on screen — visible covers are exempt from eviction.
    /// </summary>
    private const int OffScreenCovers = 40;

    private readonly Dictionary<Guid, string> _uris = [];
    private readonly HashSet<Guid> _queued = [];      // queued or already attempted this session
    private readonly HashSet<Guid> _fetching = [];    // still to finish — drives the loading placeholder
    private readonly Dictionary<Guid, SongListItem> _wanted = [];   // asked for, waiting to become visible
    private readonly HashSet<Guid> _visible = [];     // reported by the viewport observer
    private readonly Dictionary<Guid, long> _leftView = [];   // when each cover last went off screen, for eviction
    private readonly Queue<SongListItem> _pending = new();
    private long _tick;
    private bool _draining;

    /// <inheritdoc />
    public event EventHandler? Changed;

    // The cached cover, or null — and, when null, what queues the fetch. Surfaces go through ViewFor.
    private string? UriFor(SongListItem song)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (!settings.AlbumArtEnabled)
            return null;

        if (_uris.TryGetValue(song.Id, out var uri))
            return uri;

        // Nothing to chase if the song has no cover and we've already been told so.
        var findable = song.ArtworkUrl is not null || !song.ArtworkLookedUp;
        if (!findable || _queued.Contains(song.Id))
            return null;

        // Remember the ask, but don't fetch until the viewport observer says this song is actually on screen.
        // Rendering is not the same as being seen: My Songs keeps hundreds of scrolled-past cards in the DOM.
        _wanted[song.Id] = song;
        if (_visible.Contains(song.Id))
            Enqueue(song);
        return null;
    }

    /// <inheritdoc />
    public AlbumArtView ViewFor(SongListItem song)
    {
        ArgumentNullException.ThrowIfNull(song);
        var uri = UriFor(song);
        if (uri is not null)
            return new AlbumArtView($"--kh-card-art: url('{uri}');", Loading: false);

        // ArtworkUrl is the "we know there's an image coming" part: before discovery runs it's null, and
        // promising a cover we might not find would flash a placeholder across most of the list.
        var loading = settings.AlbumArtEnabled
            && song.ArtworkUrl is not null
            && _fetching.Contains(song.Id);
        return loading ? new AlbumArtView(null, Loading: true) : AlbumArtView.None;
    }

    /// <inheritdoc />
    public Task SetVisibleAsync(IReadOnlyCollection<Guid> songIds)
    {
        ArgumentNullException.ThrowIfNull(songIds);

        var gone = _visible.Where(id => !songIds.Contains(id)).ToList();
        foreach (var id in gone)
        {
            _visible.Remove(id);
            _leftView[id] = ++_tick;   // eviction order is "longest off screen"
        }

        var started = false;
        foreach (var id in songIds)
        {
            if (!_visible.Add(id))
                continue;
            _leftView.Remove(id);
            if (_wanted.TryGetValue(id, out var song) && !_queued.Contains(id) && !_uris.ContainsKey(id))
            {
                Enqueue(song);
                started = true;
            }
        }

        if (started)
            Changed?.Invoke(this, EventArgs.Empty);   // paint the placeholders for what just started
        return EvictOffScreenAsync();
    }

    /// <inheritdoc />
    public async Task ObserveAsync()
    {
        if (!settings.AlbumArtEnabled)
            return;
        _selfRef ??= DotNetObjectReference.Create(this);
        try
        {
            await js.InvokeVoidAsync("khArtVisibility.register", _selfRef, new { method = nameof(VisibleArtChanged) });
        }
        catch (JSException ex)
        {
            _log.LogWarning(ex, "Wiring the album-art viewport observer failed");
        }
    }

    /// <summary>Called by the viewport observer with the ids currently on screen.</summary>
    [JSInvokable]
    public Task VisibleArtChanged(string[] songIds)
    {
        var ids = new List<Guid>(songIds.Length);
        foreach (var id in songIds)
        {
            if (Guid.TryParse(id, out var guid))
                ids.Add(guid);
        }
        return SetVisibleAsync(ids);
    }

    public void Dispose() => _selfRef?.Dispose();

    private void Enqueue(SongListItem song)
    {
        if (!_queued.Add(song.Id))
            return;
        _fetching.Add(song.Id);
        _pending.Enqueue(song);
        StartDraining();
    }

    /// <inheritdoc />
    public async Task DropAsync(Guid songId)
    {
        if (_uris.Remove(songId))
            await RevokeAsync(songId);
        _queued.Remove(songId);        // let the next request re-fetch it
        _fetching.Remove(songId);
        _wanted.Remove(songId);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        _uris.Clear();
        _queued.Clear();
        _fetching.Clear();
        _pending.Clear();
        _wanted.Clear();
        _leftView.Clear();
        _visible.Clear();   // the observer re-reports what's on screen on its next pass
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
                    // Done one way or another — the placeholder must come down.
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

        if (song.ArtworkUrl is null)
        {
            if (song.ArtworkLookedUp)
                return;
            await DiscoverArtworkUrlAsync(song);
            await Task.Delay(DiscoveryPause);
            if (song.ArtworkUrl is null)
                return;
            // A cover is now known to be coming — repaint so the placeholder shows during the download.
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

    // iTunes first; Deezer only when iTunes has no cover. The attempt is recorded hit or miss, so a coverless
    // song is never re-chased.
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

    // Visible covers are untouchable — a cap that evicts what's still rendered thrashes, because the next
    // render immediately re-asks (see DEVELOPMENT.md → Design notes).
    private async Task EvictOffScreenAsync()
    {
        var offScreen = _uris.Keys.Where(id => !_visible.Contains(id)).ToList();
        if (offScreen.Count <= OffScreenCovers)
            return;

        foreach (var id in offScreen
            .OrderBy(id => _leftView.TryGetValue(id, out var at) ? at : 0)
            .Take(offScreen.Count - OffScreenCovers))
        {
            _uris.Remove(id);
            _queued.Remove(id);     // evicted, not failed — coming back into view should re-fetch it
            _fetching.Remove(id);
            _leftView.Remove(id);
            await RevokeAsync(id);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RevokeAsync(Guid songId)
    {
        try { await js.InvokeVoidAsync("khAlbumArt.revoke", songId.ToString()); }
        catch (JSException ex) { _log.LogWarning(ex, "Revoking the album-art blob URL for {SongId} failed", songId); }
    }
}
