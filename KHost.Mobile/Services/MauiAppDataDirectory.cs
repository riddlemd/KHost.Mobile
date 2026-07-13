using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>Backed by MAUI's <see cref="FileSystem.AppDataDirectory"/> — the per-platform private data folder the
/// framework creates for the app.</remarks>
public sealed class MauiAppDataDirectory : IAppDataDirectory
{
    /// <inheritdoc />
    public string AppDataDirectory => FileSystem.AppDataDirectory;
}
