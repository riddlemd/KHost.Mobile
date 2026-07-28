using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// The tmp file MUST sit in the same directory as the target so the rename is same-volume, which is what makes
/// it atomic on every platform shipped; a temp-dir tmp would quietly become a non-atomic cross-volume copy.
/// No <c>ConfigureAwait</c> — the callers are the UI-thread JSON stores relying on the Blazor sync context.
/// </remarks>
// logger optional so AtomicWriteTests can `new` it; DI supplies the real one.
internal sealed class AtomicFileWriter(ILogger<AtomicFileWriter>? logger = null) : IAtomicFileWriter
{
    private readonly ILogger _log = logger ?? NullLogger<AtomicFileWriter>.Instance;

    /// <inheritdoc />
    public async Task WriteAsync(string path, Func<Stream, Task> writeContents)
    {
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
            await writeContents(stream);
        File.Move(tmp, path, overwrite: true);
    }

    /// <inheritdoc />
    public bool Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            var corrupt = path + ".corrupt";
            File.Move(path, corrupt, overwrite: true);
            // Previously silent: the app degraded to empty state with nothing said, which is exactly the case
            // someone needs a breadcrumb for.
            _log.LogWarning("Quarantined unparseable {Path} to {Corrupt}; starting from empty", path, corrupt);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Couldn't quarantine {Path}; starting from empty and leaving it in place", path);
            return false;
        }
    }
}
