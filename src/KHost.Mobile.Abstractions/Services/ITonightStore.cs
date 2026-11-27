using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// The "Tonight" on-deck set list — an ordered, persisted queue of songs with a per-entry completed flag. Its own
/// store (separate from <see cref="ISongListStore"/>) so the ephemeral set and its completion state are decoupled
/// from the song entities. Entries reference a <see cref="SongListItem"/> by id; orphans (whose song was deleted)
/// are dropped by <see cref="PruneAsync"/>, which callers must invoke — it never runs automatically on load/save.
/// <para>Auto-expire (<see cref="IAppSettings.TonightExpiryHours"/>) is checked when the Tonight tab opens, via
/// <see cref="ExpireIfStaleAsync"/>. <see cref="AddAsync"/> also checks it first, so a song added from My Songs the
/// next day starts a fresh set instead of restarting the clock on last night's.</para>
/// </summary>
public interface ITonightStore
{
    /// <summary>Current set, ordered by <see cref="TonightEntry.Order"/>.</summary>
    Task<IReadOnlyList<TonightEntry>> GetAllAsync();

    /// <summary>Append a song at the end of the set. No-op if it's already queued.</summary>
    Task AddAsync(Guid songId);

    /// <summary>Remove a song from the set (renumbers the rest). No-op if it isn't queued.</summary>
    Task RemoveAsync(Guid songId);

    /// <summary>Rewrite the set order to match <paramref name="orderedSongIds"/> (ids not present are ignored).</summary>
    Task ReorderAsync(IReadOnlyList<Guid> orderedSongIds);

    /// <summary>Set (or clear) a song's completed flag, stamping <see cref="TonightEntry.CompletedAt"/> and
    /// <see cref="TonightEntry.CompletedPerformanceId"/> (the performance the check-off logged) accordingly.
    /// Pass the logged performance's id when completing; it's cleared when un-completing.</summary>
    Task SetCompletedAsync(Guid songId, bool completed, Guid? performanceId = null);

    /// <summary>Clear the whole set ("End night").</summary>
    Task ClearAsync();

    /// <summary>
    /// Clear the set if auto-expire is on and at least <see cref="IAppSettings.TonightExpiryHours"/> have passed
    /// since the most recently added entry's <see cref="TonightEntry.AddedAt"/>. Returns true when it cleared
    /// something. Called by the Tonight page as it loads.
    /// </summary>
    Task<bool> ExpireIfStaleAsync();

    /// <summary>Drop any entries whose song id is not in <paramref name="existingSongIds"/> (orphan pruning).</summary>
    Task PruneAsync(IReadOnlyCollection<Guid> existingSongIds);

    event EventHandler? Changed;
}
