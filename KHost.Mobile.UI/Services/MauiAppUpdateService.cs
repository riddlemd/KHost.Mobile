using KHost.Mobile.Clients.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Compares this build's <see cref="AppInfo.Current"/> version against the newest GitHub release. Singleton, and
/// the check <see cref="Task"/> is cached, so the network call happens once per launch no matter how many
/// components ask.
/// </remarks>
public sealed class MauiAppUpdateService(IUpdateLookup updateClient, IAppSettings settings, ILogger<MauiAppUpdateService> logger) : IAppUpdateService
{
    private Task<AppUpdateStatus>? _check;

    public bool Dismissed { get; private set; }

    // Blazor calls this on the single UI thread, so a plain null-coalescing memoize is race-free here.
    public Task<AppUpdateStatus> GetStatusAsync() => _check ??= RunAsync();

    public void Dismiss() => Dismissed = true;

    private async Task<AppUpdateStatus> RunAsync()
    {
        if (!settings.UpdateCheckEnabled)
            return AppUpdateStatus.None;

        if (!VersionTag.TryParse(AppInfo.Current.VersionString, out var current))
            return AppUpdateStatus.None;

        ReleaseInfo? latest;
        try
        {
            latest = await updateClient.GetNewestReleaseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update check failed; treating as no update");
            return AppUpdateStatus.None;   // best-effort: a failed check just means "nothing new"
        }

        if (latest is null)
            return AppUpdateStatus.None;

        return latest.Version > current
            ? new AppUpdateStatus(true, latest.Version.ToString(), latest.HtmlUrl)
            : AppUpdateStatus.None;
    }
}
