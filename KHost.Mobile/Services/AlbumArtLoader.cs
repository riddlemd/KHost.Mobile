using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Covers are streamed to the WebView and turned into <c>blob:</c> URLs by <c>wwwroot/js/album-art.js</c> — see
/// DEVELOPMENT.md for why that hop exists instead of serving the cached files directly.
/// </remarks>
public sealed class AlbumArtLoader(IJSRuntime js, IAlbumArtCache cache, IAppSettings settings,
    ILogger<AlbumArtLoader>? logger = null) : IAlbumArtLoader
{
    private readonly ILogger<AlbumArtLoader> _log = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AlbumArtLoader>.Instance;
    private readonly Dictionary<Guid, string> _uris = [];

    // Serializes the background passes: each call cancels the previous run's token, then the new run waits for that
    // run to fully unwind before starting — so only ONE loop ever mutates the map at a time. Two overlapping runs
    // could have a cancelled one revoke a blob the live one just installed, leaving a card on a dead blob: URL.
    private CancellationTokenSource? _cts;
    private Task _run = Task.CompletedTask;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public string? UriFor(Guid songId) =>
        settings.AlbumArtEnabled && _uris.TryGetValue(songId, out var uri) ? uri : null;

    /// <inheritdoc />
    public Task LoadAsync(IEnumerable<SongListItem> songs)
    {
        if (!settings.AlbumArtEnabled)
            return Task.CompletedTask;

        // Snapshot before going async: the caller's sequence is usually a live query over a page that may re-sort.
        var pending = songs.Where(s => s.ArtworkUrl is not null && !_uris.ContainsKey(s.Id)).ToList();
        if (pending.Count == 0)
            return Task.CompletedTask;

        _cts?.Cancel();
        var previous = _run;
        _cts = new CancellationTokenSource();
        return _run = LoadCoreAsync(pending, previous, _cts.Token);
    }

    private async Task LoadCoreAsync(List<SongListItem> pending, Task previous, CancellationToken token)
    {
        try { await previous; } catch { /* the prior run's failure is its own concern; we just needed it finished */ }
        if (token.IsCancellationRequested)
            return;

        foreach (var item in pending)
        {
            if (token.IsCancellationRequested)
                return;

            Stream? stream;
            try { stream = await cache.OpenArtStreamAsync(item.ArtworkUrl, token); }
            catch (OperationCanceledException) { return; }
            if (stream is null)
                continue;
            if (token.IsCancellationRequested)
            {
                stream.Dispose();
                return;
            }

            try
            {
                // Streamed rather than passed as a byte[], which would base64 in transit. DotNetStreamReference
                // disposes the stream once the WebView has read it.
                var objectUrl = await js.InvokeAsync<string>("khAlbumArt.set", item.Id.ToString(), new DotNetStreamReference(stream));

                // If a newer run superseded us mid-transfer, DON'T revoke: that run may already own this id and its
                // set() has revoked our now-stale blob. Just stop — the next run reconciles it.
                if (token.IsCancellationRequested)
                    return;

                _uris[item.Id] = objectUrl;
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { stream.Dispose(); return; }
            catch (Exception ex)
            {
                stream.Dispose();   // skip a cover that won't transfer; the file handle isn't leaked
                _log.LogWarning(ex, "Album art transfer failed for {SongId}", item.Id);
            }
        }
    }

    /// <inheritdoc />
    public async Task DropAsync(Guid songId)
    {
        if (!_uris.Remove(songId))
            return;
        await js.InvokeVoidAsync("khAlbumArt.revoke", songId.ToString());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        _cts?.Cancel();
        _uris.Clear();
        await js.InvokeVoidAsync("khAlbumArt.revokeAll");
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
