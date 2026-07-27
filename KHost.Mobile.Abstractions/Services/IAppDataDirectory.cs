namespace KHost.Mobile.Services;

/// <summary>
/// Supplies the directory where the app persists its JSON stores. Abstracted so the stores depend on a plain path
/// string rather than MAUI's static <c>FileSystem.AppDataDirectory</c>: that keeps them unit-testable off-device
/// (tests point this at a temp folder) while the app resolves the real per-platform private data path.
/// </summary>
public interface IAppDataDirectory
{
    /// <summary>Absolute path to the app's private data directory. The directory is expected to already exist.</summary>
    string AppDataDirectory { get; }
}
