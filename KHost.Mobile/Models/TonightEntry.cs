namespace KHost.Mobile.Models;

/// <summary>
/// One song queued in tonight's on-deck set. Links to a <see cref="SongListItem"/> by id; ordering and completion
/// are the queue's own concern, kept in a separate store so "done tonight" never leaks from a song's lifetime
/// sung-state (a song sung earlier today is still un-checked until you check it off here). Mutable class per the
/// persisted-entity convention.
/// </summary>
public sealed class TonightEntry
{
    /// <summary>The queued song — references <see cref="SongListItem.Id"/>.</summary>
    public Guid SongId { get; set; }

    /// <summary>Position in the set (0-based). Contiguous after any reorder.</summary>
    public int Order { get; set; }

    /// <summary>True once checked off this session (sung). Set independently of the song's performance history.</summary>
    public bool Completed { get; set; }

    /// <summary>When it was checked off, or null while still on deck.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The <see cref="Performance.Id"/> the check-off logged on the song, so an undo can remove exactly
    /// that performance even after an app restart (the mapping is persisted, not held in memory). Null when not
    /// completed.</summary>
    public Guid? CompletedPerformanceId { get; set; }

    /// <summary>When it was added to tonight's set.</summary>
    public DateTimeOffset AddedAt { get; set; }
}
