using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by MAUI's <see cref="Launcher"/>: hands the URL to the OS so it opens in the native browser (or a
/// matching app, e.g. YouTube) rather than the in-app WebView.
/// </remarks>
public sealed class MauiLinkLauncher(ILogger<MauiLinkLauncher> logger) : ILinkLauncher
{
    public async Task OpenAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            // Nothing on the device could handle it — don't crash the UI over a link.
            logger.LogWarning(ex, "Couldn't open link {Url}", url);
        }
    }
}
