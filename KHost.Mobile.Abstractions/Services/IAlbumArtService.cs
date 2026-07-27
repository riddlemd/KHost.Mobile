using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>Everything a surface needs to paint a song's cover.</summary>
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
public interface IAlbumArtService
{
    /// <summary>Raised when a cover lands or is dropped, so a surface can repaint. Fires on the UI context.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// How to paint this song right now: its cover if ready, a placeholder if one is coming, or nothing.
    /// Asking is what starts the work; cheap and idempotent from a render path.
    /// <para><see cref="AlbumArtView.Loading"/> stays false during discovery, while it's unknown whether a cover
    /// exists at all — most songs have none, and promising one would flash placeholders across the list.</para>
    /// </summary>
    AlbumArtView ViewFor(SongListItem song);

    /// <summary>
    /// Reports which songs are on screen, from the viewport observer. Only these are fetched, and only songs
    /// outside this set are ever evicted.
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
