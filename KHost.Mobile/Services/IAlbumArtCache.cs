namespace KHost.Mobile.Services;

/// <summary>
/// On-device cache of album-art images. Given a cover URL it ensures the image is downloaded + stored, then hands
/// back a readable stream over the cached bytes — the caller ships that to the WebView as a <c>blob:</c> object URL
/// (see <c>js/album-art.js</c>), so no megabytes of base64 live in the render tree. Everything is best-effort: any
/// failure returns null rather than throwing, so a missing cover never breaks a card.
/// </summary>
public interface IAlbumArtCache
{
    /// <summary>
    /// Ensure the image at <paramref name="url"/> is cached on device, then open a fresh readable stream over its
    /// bytes. Downloads on first request; later requests (this launch or a future one) read straight from disk. The
    /// caller owns the returned stream and must dispose it (a <c>DotNetStreamReference</c> does this after the WebView
    /// reads it). Returns null for a blank URL, an empty image, or any network/IO failure. Once cached, needs no network.
    /// </summary>
    Task<Stream?> OpenArtStreamAsync(string? url, CancellationToken cancellationToken = default);

    /// <summary>Deletes every cached image. They re-download the next time their songs are viewed.</summary>
    Task ClearAsync();

    /// <summary>Number of cached images on disk (drives the Settings "clear" button's count).</summary>
    Task<int> CountAsync();

    /// <summary>Raised after the cache grows (a new image was stored) or is cleared, so a live count stays current.</summary>
    event EventHandler? Changed;
}
