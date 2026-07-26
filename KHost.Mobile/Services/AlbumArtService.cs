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

    /// <summary>
    /// How many discovery results to hold before persisting them as one batch. Each flush rewrites the whole
    /// song-list file, so per-song writes made a cold sweep O(library²) in disk I/O; batching bounds what a
    /// crash can lose to this many re-lookups.
    /// </summary>
    private const int FlushEvery = 12;

    /// <summary>Trailing delay that folds a burst of cover arrivals into one repaint.</summary>
    private static readonly TimeSpan ChangeCoalesce = TimeSpan.FromMilliseconds(50);

    private readonly Dictionary<Guid, string> _uris = [];
    private readonly HashSet<Guid> _queued = [];      // queued or already attempted this session
    private readonly HashSet<Guid> _fetching = [];    // still to finish — drives the loading placeholder
    private readonly Dictionary<Guid, SongListItem> _wanted = [];   // asked for, waiting to become visible
    private readonly HashSet<Guid> _visible = [];     // reported by the viewport observer
    private readonly Dictionary<Guid, long> _leftView = [];   // when each cover last went off screen, for eviction
    private readonly Queue<SongListItem> _pending = new();
    private readonly List<SongListItem> _unsaved = [];   // discovery results awaiting a batched persist
    private long _tick;
    private bool _draining;
    private bool _raisePending;

    /// <inheritdoc />
    public event EventHandler? Changed;

    // Every raise goes through here: a burst of covers landing one at a time would otherwise re-render (and
    // re-sort) the whole page once per cover. Trailing-edge, so the last cover in a burst still paints.
    private void RaiseChanged()
    {
        if (_raisePending)
            return;
        _raisePending = true;
        _ = RaiseSoonAsync();
    }

    private async Task RaiseSoonAsync()
    {
        await Task.Delay(ChangeCoalesce);
        _raisePending = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

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
            RaiseChanged();   // paint the placeholders for what just started
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
        // Every clear happens BEFORE the first await. Awaiting first would let a caller that's already
        // rebuilding — a page remounting after a tab switch — register visibility and queue fetches that this
        // then wipes, leaving covers blank until a scroll re-reports them.
        var unflushed = _unsaved.ToList();
        _unsaved.Clear();
        _uris.Clear();
        _queued.Clear();
        _fetching.Clear();
        _pending.Clear();
        _wanted.Clear();
        _leftView.Clear();
        _visible.Clear();   // the observer re-reports what's on screen on its next pass

        // A singer switch lands here — persist the outgoing singer's discovery results while the store may
        // still be keyed to them. (After a re-key the batch just skips ids it can't find.)
        if (unflushed.Count > 0)
        {
            try { await store.UpdateRangeAsync(unflushed); }
            catch (Exception ex) { _log.LogWarning(ex, "Persisting {Count} artwork lookup result(s) failed", unflushed.Count); }
        }

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

                // Scrolled out of view before its turn came. Drop it rather than spend a paced lookup on
                // something nobody is looking at: the queue is sequential, so without this a fast scroll
                // back and forth buries the songs actually on screen behind everything they scrolled past,
                // and those sit on a loading placeholder for as long as the backlog takes. Clearing _queued
                // lets it re-enqueue from ViewFor/SetVisibleAsync if it comes back.
                if (!_visible.Contains(song.Id))
                {
                    _queued.Remove(song.Id);
                    if (_fetching.Remove(song.Id))
                        RaiseChanged();
                    continue;
                }

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
                        RaiseChanged();
                }
            }
        }
        finally
        {
            _draining = false;
            await FlushUnsavedAsync();   // the burst is over — persist whatever discovery learned
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
            RaiseChanged();
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
        RaiseChanged();
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
            var meta = (await metadata.LookupAsync(song.Title, song.Artist)).Match;
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

        // NOT persisted per song: each store write rewrites the whole song-list file and fires Changed (a full
        // page refresh), so a cold sweep used to cost one full-file write per undiscovered song. The flags are
        // already live in memory on the shared item; the batch is only for the next launch.
        _unsaved.Add(song);
        if (_unsaved.Count >= FlushEvery)
            await FlushUnsavedAsync();
    }

    // Losing a flush is affordable — the flags re-derive from a re-lookup — so a failed write just retries at
    // the next flush point rather than surfacing.
    private async Task FlushUnsavedAsync()
    {
        if (_unsaved.Count == 0)
            return;
        try
        {
            await store.UpdateRangeAsync(_unsaved);
            _unsaved.Clear();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Persisting {Count} artwork lookup result(s) failed; will retry at the next flush", _unsaved.Count);
        }
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
        RaiseChanged();
    }

    private async Task RevokeAsync(Guid songId)
    {
        try { await js.InvokeVoidAsync("khAlbumArt.revoke", songId.ToString()); }
        catch (JSException ex) { _log.LogWarning(ex, "Revoking the album-art blob URL for {SongId} failed", songId); }
    }
}
