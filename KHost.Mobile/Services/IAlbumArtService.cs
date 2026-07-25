using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The one way to get a song's cover. Asking for art a song doesn't have yet is what *causes* it to be
/// fetched: the service discovers the artwork URL if it isn't known, downloads and caches the image, hands it
/// to the WebView, and raises <see cref="Changed"/> when it lands.
/// </summary>
/// <remarks>
/// This replaced a loader whose callers had to pre-declare what to fetch (<c>LoadAsync(theseSongs)</c>). Every
/// surface then had to remember to do it, with the page it was showing — and any surface displaying a song from
/// outside that page (the 🎲 pick, a detail sheet) silently showed a blank card until it grew its own
/// workaround. Requests now drive the fetching, so a surface only has to render what it's given.
/// </remarks>
public interface IAlbumArtService
{
    /// <summary>Raised when a cover lands or is dropped, so a surface can repaint. Fires on the UI context.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// The <c>blob:</c> URL for this song's cover, or null when it isn't ready — which includes "not fetched
    /// yet", "the song has no cover", and "album art is switched off". A null return for a song whose art
    /// could still be found schedules that work and raises <see cref="Changed"/> once it's in.
    /// <para>Cheap and safe to call from a render path: repeat calls for the same song don't re-queue it.</para>
    /// </summary>
    string? UriFor(SongListItem song);

    /// <summary>
    /// Drops one song's cover — for an edit that changed the title/artist, so the old image can't linger. The
    /// next request re-fetches it.
    /// </summary>
    Task DropAsync(Guid songId);

    /// <summary>Drops every cover and frees its blob URL — a singer switch, or the disk cache being cleared.</summary>
    Task ClearAsync();
}
