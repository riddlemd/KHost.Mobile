using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// Everything a surface needs to paint a song's cover, so all four of them do it the same way instead of each
/// keeping their own <c>HasArt</c>/<c>ArtStyle</c> pair.
/// </summary>
/// <param name="Style">Inline <c>--kh-card-art</c> declaration when the cover is ready, else null.</param>
/// <param name="Loading">Whether a cover is on its way in and a placeholder should stand in for it.</param>
public sealed record AlbumArtView(string? Style, bool Loading)
{
    /// <summary>Whether the cover is ready to paint — the caller adds its own <c>--art</c> variant class.</summary>
    public bool HasArt => Style is not null;

    /// <summary>Nothing to show and nothing coming.</summary>
    public static AlbumArtView None { get; } = new(null, false);
}

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
    /// <summary>
    /// How to paint this song right now: its cover if ready, a placeholder if one is coming, or nothing.
    /// <para>The placeholder is deliberately not shown during the *discovery* step, when it isn't yet known
    /// whether the song has a cover at all — most songs in a real library don't, so promising one there would
    /// flash a placeholder across the majority of cards and then take it away again.</para>
    /// <para>This is the only call a surface needs — asking is what starts the work, and it's cheap enough to
    /// call from a render path.</para>
    /// </summary>
    AlbumArtView ViewFor(SongListItem song);

    /// <summary>
    /// Reports which songs are actually on screen, from the viewport observer. Only these are fetched, and only
    /// songs outside this set are ever evicted — so the cache tracks what you're looking at rather than
    /// everything you've scrolled past.
    /// </summary>
    Task SetVisibleAsync(IReadOnlyCollection<Guid> songIds);

    /// <summary>
    /// Wires the viewport observer onto any element carrying <c>data-art-song</c>, and picks up elements added
    /// since the last call. Idempotent — a page calls it from <c>OnAfterRenderAsync</c>, and a surface joins in
    /// simply by carrying the attribute.
    /// </summary>
    Task ObserveAsync();

    /// <summary>
    /// Drops one song's cover — for an edit that changed the title/artist, so the old image can't linger. The
    /// next request re-fetches it.
    /// </summary>
    Task DropAsync(Guid songId);

    /// <summary>Drops every cover and frees its blob URL — a singer switch, or the disk cache being cleared.</summary>
    Task ClearAsync();
}
