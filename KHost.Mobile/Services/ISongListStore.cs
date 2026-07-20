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
    /// Add a new entry. Title is required; artist/notes optional. New songs start unsung (a wishlist entry, no
    /// performances); sung-state and ratings are recorded later from the detail sheet.
    /// </summary>
    Task<SongListItem> AddAsync(string title, string artist, string? notes = null, string? genre = null, int? year = null);

    /// <summary>Persist edits to an existing item (matched by <see cref="SongListItem.Id"/>).</summary>
    Task UpdateAsync(SongListItem item);

    /// <summary>Bulk-persist edits to many existing items in one shot: loads once, updates each match by
    /// <see cref="SongListItem.Id"/>, saves once, and fires <see cref="Changed"/> a single time — not per
    /// item. Ids not present are skipped. Used by the post-import review to write all rows at once.</summary>
    Task UpdateRangeAsync(IEnumerable<SongListItem> items);

    /// <summary>Remove an item by id. No-op if it isn't present.</summary>
    Task RemoveAsync(Guid id);

    /// <summary>Remove every song. No-op (and no <see cref="Changed"/>) if the list is already empty.</summary>
    Task ClearAsync();

    /// <summary>Re-insert a previously removed item verbatim (same Id, timestamps, rating). No-op if it's
    /// already present. Backs swipe-to-remove Undo so a restored song returns to its exact place.</summary>
    Task RestoreAsync(SongListItem item);

    /// <summary>
    /// Bulk-add many songs in one shot (e.g. a playlist import): loads once, appends, saves once, and
    /// fires <see cref="Changed"/> a single time — not per song. When <paramref name="skipDuplicates"/>
    /// is true, incoming songs whose title+artist already exist (or repeat within the batch) are skipped.
    /// Entries with a blank title are ignored. Returns the number actually added.
    /// </summary>
    Task<int> ImportAsync(IEnumerable<SongListItem> items, bool skipDuplicates = true);

    /// <summary>
    /// Upsert many songs <em>by <see cref="SongListItem.Id"/></em>, preserving their ids — the singer-profile
    /// restore path. Existing ids are replaced in place (verbatim, history and all); new ids are appended.
    /// One load, one save, one <see cref="Changed"/>. Unlike <see cref="ImportAsync"/> it does <b>not</b> dedupe
    /// by title+artist — the profile is the source of truth for these ids. Blank-title entries are ignored.
    /// Returns the number of rows written (added + replaced).
    /// </summary>
    Task<int> MergeByIdAsync(IEnumerable<SongListItem> items);
}
