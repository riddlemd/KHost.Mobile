using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The one shared reconcile implementation for cloud sync — pure, platform-agnostic, side-effect-free, so both
/// the Android (Google Drive) and iOS (iCloud) backends merge identically. Model: <b>last-write-wins per item</b>,
/// keyed on <see cref="SongListItem.Id"/> and decided by <see cref="SongListItem.UpdatedAt"/>, with
/// <see cref="SongListItem.DeletedAt"/> tombstones so deletions propagate instead of a pulled copy resurrecting a
/// locally-removed row. This is item-granular, not field-granular: two devices editing the *same* item concurrently
/// don't field-merge — the newer whole item wins. That's a deliberate simplification for a single-user wishlist.
/// </summary>
public static class SyncMerge
{
    /// <summary>
    /// Reconcile the full local snapshot (including tombstones) with the full remote snapshot (may be null on the
    /// first-ever sync) into a single authoritative snapshot — the exact list to persist locally AND push back to the
    /// cloud. Tombstones are retained so they keep propagating; <see cref="Compact"/> purges the stale ones later.
    /// </summary>
    public static IReadOnlyList<SongListItem> Merge(
        IReadOnlyList<SongListItem> local,
        IReadOnlyList<SongListItem>? remote)
    {
        ArgumentNullException.ThrowIfNull(local);

        var merged = new Dictionary<Guid, SongListItem>(local.Count);
        foreach (var item in local)
            merged[item.Id] = item;

        if (remote is not null)
        {
            foreach (var incoming in remote)
            {
                if (!merged.TryGetValue(incoming.Id, out var current))
                {
                    merged[incoming.Id] = incoming;   // remote-only item (incl. a remote deletion we've never seen)
                    continue;
                }

                merged[incoming.Id] = Winner(current, incoming);
            }
        }

        return merged.Values.ToList();
    }

    /// <summary>Last-write-wins between two copies of the same item. Newer <see cref="SongListItem.UpdatedAt"/> wins;
    /// on an exact tie a deletion beats a live edit (a delete shouldn't be silently undone), and a remaining tie keeps
    /// the local copy for determinism.</summary>
    private static SongListItem Winner(SongListItem local, SongListItem remote)
    {
        var cmp = remote.UpdatedAt.CompareTo(local.UpdatedAt);
        if (cmp > 0)
            return remote;
        if (cmp < 0)
            return local;

        // Equal timestamps: prefer whichever is a tombstone; else keep local.
        if (remote.DeletedAt is not null && local.DeletedAt is null)
            return remote;
        return local;
    }

    /// <summary>
    /// A stable content signature of a snapshot — the per-item identity that sync cares about (id + last-edit stamp +
    /// tombstone state). Two snapshots with equal signatures need no write, so the coordinator uses this to skip a
    /// redundant local re-save or a needless remote push (which would burn a Version bump).
    /// </summary>
    public static string Signature(IReadOnlyList<SongListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return string.Join('|', items
            .OrderBy(i => i.Id)
            .Select(i => $"{i.Id:N}:{i.UpdatedAt.UtcTicks}:{(i.DeletedAt?.UtcTicks ?? 0)}"));
    }

    /// <summary>
    /// Drop tombstones older than <paramref name="maxTombstoneAge"/> (default 30 days). Runs after a successful sync
    /// so a deletion has had time to reach the user's other devices before its tombstone is forgotten. Live items are
    /// always kept.
    /// </summary>
    public static IReadOnlyList<SongListItem> Compact(
        IReadOnlyList<SongListItem> items,
        DateTimeOffset now,
        TimeSpan? maxTombstoneAge = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var cutoff = now - (maxTombstoneAge ?? TimeSpan.FromDays(30));
        return items.Where(i => i.DeletedAt is null || i.DeletedAt.Value > cutoff).ToList();
    }
}
