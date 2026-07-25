using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The cover images currently handed to the WebView, keyed by song. Shared so a cover fetched for one screen is
/// already there on the next — My Songs and Tonight show the same songs, and a per-page cache would fetch, decode
/// and hold each image twice.
/// </summary>
public interface IAlbumArtLoader
{
    /// <summary>Raised when a cover finishes loading, so a page can paint it without polling.</summary>
    event EventHandler? Changed;

    /// <summary>The <c>blob:</c> URL for a song's cover, or null when it isn't loaded (or art is switched off).</summary>
    string? UriFor(Guid songId);

    /// <summary>
    /// Loads covers for <paramref name="songs"/> that have an artwork URL and aren't loaded yet, in the background.
    /// Callers pass only what's on screen, so image memory stays bounded by what's visible. Each call supersedes the
    /// previous one.
    /// </summary>
    Task LoadAsync(IEnumerable<SongListItem> songs);

    /// <summary>Drops one song's cover — for an edit that changed the title/artist, so the old image can't linger.</summary>
    Task DropAsync(Guid songId);

    /// <summary>Drops every cover and frees its blob URL. Used on a singer switch, whose song ids won't recur.</summary>
    Task ClearAsync();
}
