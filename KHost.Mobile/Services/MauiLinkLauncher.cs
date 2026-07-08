using Microsoft.Maui.ApplicationModel;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ILinkLauncher"/> backed by MAUI's <see cref="Launcher"/>: hands the URL to the OS so it opens
/// in the native browser (or a matching app, e.g. YouTube) rather than the in-app WebView.
/// </summary>
public sealed class MauiLinkLauncher : ILinkLauncher
{
    public async Task OpenAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch
        {
            // Nothing on the device could handle it — don't crash the UI over a link.
        }
    }
}
