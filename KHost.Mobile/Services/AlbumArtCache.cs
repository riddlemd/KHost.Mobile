using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IAlbumArtCache"/> that stores each cover as a raw image file under an <c>album-art</c> subfolder of
/// the app data directory, named by a hash of its source URL (so identical covers dedupe and re-lookup is a no-op).
/// A blob store rather than the JSON pattern the other caches use, because the payload is binary image bytes.
/// </summary>
/// <remarks>
/// Images are served to the Blazor WebView as base64 <c>data:</c> URIs — the pragmatic cross-platform way to show a
/// device-local file in the WebView without a custom URL scheme. The bytes are downloaded from the artwork CDN (not
/// the rate-limited iTunes Search API), so caching the whole visible list is fine. An in-memory memo avoids
/// re-reading + re-encoding a cover that's already been served this launch.
/// </remarks>
public sealed class AlbumArtCache(IAppDataDirectory paths, IHttpClientFactory httpFactory, ILogger<AlbumArtCache>? logger = null) : IAlbumArtCache
{
    private readonly string _dir = Path.Combine(paths.AppDataDirectory, "album-art");
    private readonly SemaphoreSlim _gate = new(1, 1);                       // guards count/clear against the folder
    private readonly Dictionary<string, string> _memo = new(StringComparer.Ordinal);   // url -> data URI
    // Optional so the integration tests can `new` the cache without a logging stack; DI supplies the real logger.
    private readonly ILogger _log = logger ?? NullLogger<AlbumArtCache>.Instance;

    public event EventHandler? Changed;

    public async Task<string?> GetDataUriAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        lock (_memo)
        {
            if (_memo.TryGetValue(url, out var cached))
                return cached;
        }

        var path = PathFor(url);
        byte[] bytes;
        var wroteNew = false;
        try
        {
            if (File.Exists(path))
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                _log.LogDebug("Album art served from disk cache ({Bytes} bytes) for {Url}", bytes.Length, url);
            }
            else
            {
                var http = httpFactory.CreateClient("album-art");
                bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                Directory.CreateDirectory(_dir);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
                wroteNew = true;
                _log.LogDebug("Album art downloaded + cached ({Bytes} bytes) for {Url}", bytes.Length, url);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: a failed download/read just means no art for this card. Cancellation propagates.
            _log.LogWarning(ex, "Album art download/read failed for {Url}; the card stays blank", url);
            return null;
        }

        if (bytes.Length == 0)
        {
            _log.LogWarning("Album art for {Url} was empty (0 bytes); the card stays blank", url);
            return null;
        }

        // iTunes covers are JPEG; the mime only needs to be image/* for the WebView to render it.
        var dataUri = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
        lock (_memo)
        {
            _memo[url] = dataUri;
        }

        if (wroteNew)
            Changed?.Invoke(this, EventArgs.Empty);

        return dataUri;
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            lock (_memo)
                _memo.Clear();

            if (Directory.Exists(_dir))
            {
                foreach (var file in Directory.EnumerateFiles(_dir))
                {
                    try { File.Delete(file); changed = true; }
                    catch { /* a locked/vanished file shouldn't abort clearing the rest */ }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> CountAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return Directory.Exists(_dir) ? Directory.EnumerateFiles(_dir).Count() : 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Stable per-URL filename: SHA-256 of the URL, hex. Keeps arbitrary URL characters out of the filesystem and
    // dedupes two songs that share a cover. No extension needed — the bytes are read back verbatim.
    private string PathFor(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Path.Combine(_dir, Convert.ToHexString(hash));
    }
}
