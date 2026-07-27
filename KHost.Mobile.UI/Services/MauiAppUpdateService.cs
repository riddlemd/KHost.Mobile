using KHost.Mobile.Abstractions.Clients.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using KHost.Mobile.Abstractions.Services;

namespace KHost.Mobile.UI.Services;

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

        // Plain Version.TryParse, not the GitHub tag normalizer: this string is ApplicationDisplayVersion,
        // which we set and which has always been plain dotted numerics. Giving it a "v" or a "-beta" suffix
        // would land here as a silent no-update — so if that ever changes, change this too.
        if (!Version.TryParse(AppInfo.Current.VersionString, out var current))
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
