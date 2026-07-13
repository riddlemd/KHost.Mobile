namespace KHost.Mobile.Services;

/// <summary>
/// On-device cache of album-art images. Given a cover URL it returns a ready-to-render <c>data:</c> URI,
/// downloading + storing the bytes on first use and serving them from disk (and an in-memory memo) afterwards.
/// Everything is best-effort: any failure returns null rather than throwing, so a missing cover never breaks a card.
/// </summary>
public interface IAlbumArtCache
{
    /// <summary>
    /// Returns a <c>data:image/jpeg;base64,…</c> URI for the image at <paramref name="url"/> — suitable to drop
    /// straight into a CSS <c>background-image</c>. Downloads and caches the bytes on first request; later requests
    /// (this launch or a future one) read from the on-device cache. Returns null for a blank URL or any
    /// network/IO failure. Once cached, this needs no network.
    /// </summary>
    Task<string?> GetDataUriAsync(string? url, CancellationToken cancellationToken = default);

    /// <summary>Deletes every cached image. They re-download the next time their songs are viewed.</summary>
    Task ClearAsync();

    /// <summary>Number of cached images on disk (drives the Settings "clear" button's count).</summary>
    Task<int> CountAsync();

    /// <summary>Raised after the cache grows (a new image was stored) or is cleared, so a live count stays current.</summary>
    event EventHandler? Changed;
}
