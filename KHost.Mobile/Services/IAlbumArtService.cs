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
    /// True while a cover the song is known to have is on its way in — i.e. the image is being downloaded and
    /// will appear. Surfaces use it to show a placeholder instead of a bare card.
    /// <para>Deliberately false during the *discovery* step, when it isn't yet known whether the song has a cover
    /// at all: most songs in a real library don't, so promising one there would flash a placeholder on the
    /// majority of cards and then take it away again.</para>
    /// </summary>
    bool IsFetching(SongListItem song);

    /// <summary>
    /// Drops one song's cover — for an edit that changed the title/artist, so the old image can't linger. The
    /// next request re-fetches it.
    /// </summary>
    Task DropAsync(Guid songId);

    /// <summary>Drops every cover and frees its blob URL — a singer switch, or the disk cache being cleared.</summary>
    Task ClearAsync();
}
