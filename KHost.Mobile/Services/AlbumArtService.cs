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
    ILogger<AlbumArtService>? logger = null) : IAlbumArtService, IDisposable
{
    private DotNetObjectReference<AlbumArtService>? _selfRef;

    private readonly ILogger<AlbumArtService> _log =
        logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AlbumArtService>.Instance;

    /// <summary>
    /// Pause after each lookup that actually went to the network. Most libraries are mostly coverless (~80%), so
    /// without this a sweep fires back-to-back iTunes calls and earns a rate-limit block. Downloads of an
    /// already-known cover URL hit the artwork CDN instead and aren't paced.
    /// </summary>
    private static readonly TimeSpan DiscoveryPause = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// How many covers to hold once they've scrolled away. Only songs that are NOT currently on screen are ever
    /// evicted, so this is headroom for scrolling back, not a limit on what you can see at once.
    /// <para>An earlier cap keyed off what was <em>rendered</em> rather than visible, and thrashed: My Songs
    /// keeps every card you've scrolled past in the DOM, so evicting one had the next render ask for it straight
    /// back. The viewport set is what makes a cap safe.</para>
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

    // The cached cover, or null — and, when there isn't one, the thing that gets it moving. Private because
    // ViewFor is the surfaces' single entry point; splitting "the URL" from "is it loading" is what let the
    // detail sheet quietly miss the loading state.
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
            // Came into view and someone had asked for it — now it's worth fetching.
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

    // Drops the covers that have been off screen longest, once there are more of them than the headroom allows.
    // Anything currently visible is untouchable, which is the whole reason this can't thrash the way a
    // render-count-based cap did: the next render can only re-ask for what's on screen, and that's never evicted.
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
