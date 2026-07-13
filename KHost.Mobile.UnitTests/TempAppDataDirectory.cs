using KHost.Mobile.Services;

namespace KHost.Mobile.UnitTests;

/// <summary>
/// A throwaway <see cref="IAppDataDirectory"/> backed by a fresh, unique temp folder — the "fake FileSystem" the
/// JSON stores write into. One per test so every store instance is fully isolated; the folder (and every store
/// file in it) is deleted on <see cref="Dispose"/>.
/// </summary>
public sealed class TempAppDataDirectory : IAppDataDirectory, IDisposable
{
    public string AppDataDirectory { get; }

    public TempAppDataDirectory()
    {
        AppDataDirectory = Path.Combine(Path.GetTempPath(), "khost-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(AppDataDirectory);
    }

    /// <summary>Absolute path to a store file inside the fake directory — lets a test seed or corrupt the raw JSON
    /// before a store reads it, exercising the load/migrate/corrupt-file paths directly.</summary>
    public string FilePath(string fileName) => Path.Combine(AppDataDirectory, fileName);

    public void Dispose()
    {
        // Best-effort cleanup: a leaked temp dir is harmless, so don't let a locked file fail the test run.
        try { Directory.Delete(AppDataDirectory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
