namespace KHost.Mobile.Services;

/// <summary>
/// The app-facing "is there a newer version?" check. It runs at most <b>once per application launch</b>
/// (the result is memoized) and remembers a one-tap dismissal for the rest of the session. Respects the
/// <see cref="IAppSettings.UpdateCheckEnabled"/> toggle — when off, it never touches the network.
/// </summary>
public interface IAppUpdateService
{
    /// <summary>
    /// The update status for this launch. The first call performs the network check (honoring the setting);
    /// every later call returns the same cached result. Never throws — failures surface as
    /// <see cref="AppUpdateStatus.None"/>.
    /// </summary>
    Task<AppUpdateStatus> GetStatusAsync();

    /// <summary>Hide the update prompt for the rest of this session.</summary>
    void Dismiss();

    /// <summary>True once <see cref="Dismiss"/> has been called this session.</summary>
    bool Dismissed { get; }
}

/// <summary>Outcome of the launch update check.</summary>
public sealed record AppUpdateStatus(bool UpdateAvailable, string? LatestVersion, string? ReleaseUrl)
{
    /// <summary>No update (up to date, check disabled, or the check couldn't complete).</summary>
    public static readonly AppUpdateStatus None = new(false, null, null);
}
