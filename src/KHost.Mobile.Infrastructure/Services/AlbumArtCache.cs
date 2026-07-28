using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// Each cover is a raw image file under an <c>album-art</c> subfolder of the app data directory, named by a hash of
/// its source URL, so identical covers dedupe. A blob store rather than the JSON pattern the other caches use,
/// because the payload is binary. No in-memory copy is kept: repeat requests re-open the on-disk file (cheap) and
/// the browser caches the decoded image behind its object URL. Downloads hit the artwork CDN, not the rate-limited
/// iTunes Search API.
/// </remarks>
internal sealed class AlbumArtCache(IAppDataDirectory paths, IHttpClientFactory httpFactory, IAtomicFileWriter? writer = null, ILogger<AlbumArtCache>? logger = null) : IAlbumArtCache
{
    private readonly string _dir = Path.Combine(paths.AppDataDirectory, "album-art");
    private readonly SemaphoreSlim _gate = new(1, 1);                       // guards count/clear against the folder
    // Optional so the integration tests can `new` the cache without a logging stack; DI supplies the real logger.
    private readonly ILogger _log = logger ?? NullLogger<AlbumArtCache>.Instance;

    public event EventHandler? Changed;

    public async Task<Stream?> OpenArtStreamAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var path = PathFor(url);
        var wroteNew = false;
        try
        {
            if (!File.Exists(path))
            {
                var http = httpFactory.CreateClient("album-art");
                var bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    _log.LogWarning("Album art for {Url} was empty (0 bytes); the card stays blank", url);
                    return null;
                }

                Directory.CreateDirectory(_dir);
                // Atomic (.tmp + rename) like the JSON stores, and for a sharper reason here: "is it cached?" is
                // File.Exists, so a torn write would leave a truncated image that counts as a hit FOREVER — the
                // card renders broken and never re-downloads. A failed atomic write leaves no target file at all.
                await (writer ?? new AtomicFileWriter()).WriteAsync(path, stream => stream.WriteAsync(bytes, cancellationToken).AsTask())
                    .ConfigureAwait(false);
                wroteNew = true;
                _log.LogDebug("Album art downloaded + cached ({Bytes} bytes) for {Url}", bytes.Length, url);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: a failed download just means no art for this card. Cancellation propagates.
            _log.LogWarning(ex, "Album art download failed for {Url}; the card stays blank", url);
            return null;
        }

        if (wroteNew)
            Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            var stream = File.OpenRead(path);
            // Still worth checking: covers written before the atomic-write fix above could be 0-byte or truncated.
            if (stream.Length == 0)
            {
                stream.Dispose();
                return null;
            }
            return stream;   // the caller (a DotNetStreamReference) disposes it once the WebView has read it
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Album art read failed for {Url}; the card stays blank", url);
            return null;
        }
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            if (Directory.Exists(_dir))
            {
                foreach (var file in Directory.EnumerateFiles(_dir))
                {
                    try { File.Delete(file); changed = true; }
                    catch (Exception ex)
                    {
                        // A locked/vanished file shouldn't abort clearing the rest.
                        _log.LogWarning(ex, "Couldn't delete cached album art {Path}", file);
                    }
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

    // SHA-256 of the URL, hex: keeps arbitrary URL characters out of the filesystem and dedupes two songs that
    // share a cover. No extension needed — the bytes are read back verbatim.
    private string PathFor(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Path.Combine(_dir, Convert.ToHexString(hash));
    }
}
