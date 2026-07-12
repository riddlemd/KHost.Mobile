using KHost.Mobile.Client.Updates;
using Microsoft.Maui.ApplicationModel;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IAppUpdateService"/> that compares this build's <see cref="AppInfo.Current"/> version against
/// the newest GitHub release (via <see cref="IUpdateClient"/>). Registered as a singleton so the check —
/// and the session dismissal — are shared app-wide; the check <see cref="Task"/> is cached so the network
/// call happens once per launch no matter how many components ask.
/// </summary>
public sealed class MauiAppUpdateService(IUpdateClient updateClient, IAppSettings settings) : IAppUpdateService
{
    private Task<AppUpdateStatus>? _check;   // memoized on first request; the network call runs once per launch

    public bool Dismissed { get; private set; }

    // Blazor calls this on the single UI thread, so a plain null-coalescing memoize is race-free here.
    public Task<AppUpdateStatus> GetStatusAsync() => _check ??= RunAsync();

    public void Dismiss() => Dismissed = true;

    private async Task<AppUpdateStatus> RunAsync()
    {
        if (!settings.UpdateCheckEnabled)
            return AppUpdateStatus.None;

        if (!GitHubReleaseParser.TryParseVersion(AppInfo.Current.VersionString, out var current, out _))
            return AppUpdateStatus.None;

        ReleaseInfo? latest;
        try
        {
            latest = await updateClient.GetNewestReleaseAsync().ConfigureAwait(false);
        }
        catch
        {
            return AppUpdateStatus.None;   // best-effort: a failed check just means "nothing new"
        }

        if (latest is null || !GitHubReleaseParser.TryParseVersion(latest.Version, out var latestVersion, out _))
            return AppUpdateStatus.None;

        return latestVersion > current
            ? new AppUpdateStatus(true, latest.Version, latest.HtmlUrl)
            : AppUpdateStatus.None;
    }
}
