using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The patron's on-device song list. UI binds to this interface only; today it's backed by a local JSON file, and
/// a future implementation can sync to KHost.Online behind the same contract with no UI change.
/// </summary>
public interface ISongListStore
{
    /// <summary>Raised after any mutation so components can refresh. Fired on the caller's context.</summary>
    event EventHandler? Changed;

    /// <summary>All saved songs, newest first.</summary>
    Task<IReadOnlyList<SongListItem>> GetAllAsync();

    /// <summary>
    /// Add a new entry. Title is required; artist/notes optional. When <paramref name="hasSung"/> is true the item
    /// starts as <see cref="SongListItemStatus.Sang"/> with a timestamp and the (1-5, clamped) confidence applies;
    /// otherwise it's a not-yet-sung wishlist entry and confidence is irrelevant until it's marked sung.
    /// </summary>
    Task<SongListItem> AddAsync(string title, string artist, string? notes = null, string? genre = null, bool hasSung = false, int confidence = 3);

    /// <summary>Persist edits to an existing item (matched by <see cref="SongListItem.Id"/>).</summary>
    Task UpdateAsync(SongListItem item);

    /// <summary>Remove an item by id. No-op if it isn't present.</summary>
    Task RemoveAsync(Guid id);
}
