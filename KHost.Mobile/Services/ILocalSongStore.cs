using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The device-local store, extended with the raw access the sync coordinator needs. The UI never touches this — it
/// binds to <see cref="ISongListStore"/>, whose <see cref="ISongListStore.GetAllAsync"/> hides tombstones. Sync, by
/// contrast, must see and write the <i>whole</i> snapshot (tombstones included) so deletions propagate. Same singleton
/// instance is registered under both interfaces.
/// </summary>
public interface ILocalSongStore : ISongListStore
{
    /// <summary>Every persisted item, <b>including soft-deleted tombstones</b>, in storage order. This is the local
    /// half of a merge — the cloud snapshot is the other half.</summary>
    Task<IReadOnlyList<SongListItem>> GetRawAsync();

    /// <summary>
    /// Fold a sync result back into local storage, <b>atomically merging it into whatever is stored right now</b> —
    /// not a blind overwrite. Critical for correctness: a sync's <paramref name="syncResult"/> was computed from a
    /// snapshot taken before the (possibly slow) network pull, so the user may have added/edited/removed a song in the
    /// meantime. Re-merging under the store lock lets that concurrent edit win by <see cref="SongListItem.UpdatedAt"/>
    /// instead of being clobbered (the pending debounced sync then pushes it). Fires <see cref="ISongListStore.Changed"/>
    /// once, and only when the merge actually changes what's stored.
    /// </summary>
    Task ApplyMergedAsync(IReadOnlyList<SongListItem> syncResult);
}
