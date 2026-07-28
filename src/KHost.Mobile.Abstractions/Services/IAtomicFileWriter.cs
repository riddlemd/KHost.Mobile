namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Crash-safe file writes. A write goes to a sibling <c>.tmp</c> file and is then atomically renamed over the
/// target, so an app kill or power loss leaves the <em>last good</em> file intact rather than a truncated one.
/// </summary>
/// <remarks>
/// That matters because a store's load path treats a corrupt file as "start empty" — with a direct overwrite,
/// an interrupted write would silently lose the whole list.
/// </remarks>
public interface IAtomicFileWriter
{
    /// <summary>
    /// Writes through a <c>.tmp</c> sibling, then moves it over <paramref name="path"/>. The stream is flushed
    /// and disposed before the move.
    /// </summary>
    Task WriteAsync(string path, Func<Stream, Task> writeContents);

    /// <summary>
    /// Moves a file that failed to parse aside to a <c>.corrupt</c> sibling, so the bad bytes survive the next
    /// (empty) save instead of being overwritten. Best-effort: returns true only when a file was moved aside.
    /// </summary>
    bool Quarantine(string path);
}
