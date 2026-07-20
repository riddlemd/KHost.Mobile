namespace KHost.Mobile.Services;

/// <summary>
/// Crash-safe file writes for the JSON stores. Serializes to a sibling <c>.tmp</c> file, then atomically renames
/// it over the target — so a write interrupted by an app kill or power loss leaves the <em>last good</em> file
/// intact instead of a half-written one. That matters because the stores' load path treats a corrupt (truncated)
/// file as "start empty", which for a direct-overwrite write would silently lose the whole list.
/// </summary>
/// <remarks>
/// The tmp file MUST sit in the same directory as the target so the rename is a same-volume operation (atomic on
/// every platform we ship); a temp-dir tmp would quietly become a non-atomic cross-volume copy. No <c>ConfigureAwait</c>
/// here — the callers are the UI-thread JSON stores that rely on the Blazor sync context (matching their convention).
/// </remarks>
internal static class AtomicFile
{
    /// <summary>Write via a <c>.tmp</c> sibling then atomically move it over <paramref name="path"/>. The stream is
    /// disposed (flushed) before the move.</summary>
    public static async Task WriteAsync(string path, Func<Stream, Task> writeContents)
    {
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
            await writeContents(stream);
        File.Move(tmp, path, overwrite: true);   // same-volume rename → atomic; the previous file survives a crash
    }

    /// <summary>Move a file that failed to parse aside to a <c>.corrupt</c> sibling instead of leaving it to be
    /// silently overwritten by the next (empty) save — so the bad bytes are preserved for possible recovery and
    /// a load that quarantines then starts empty never erases the only copy. Best-effort; failures are ignored.</summary>
    public static void Quarantine(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch
        {
            // A locked/vanished file just isn't quarantined — the caller still starts empty, as before.
        }
    }
}
