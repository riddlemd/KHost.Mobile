namespace KHost.Mobile.Services;

/// <summary>
/// Opens an external URL <em>outside</em> the app's WebView, letting the OS pick the handler (native
/// browser, or a matching app such as YouTube). UI binds to this interface only; a future PWA build can
/// swap in a <c>window.open</c> implementation with no UI change.
/// </summary>
public interface ILinkLauncher
{
    /// <summary>Open <paramref name="url"/> externally. No-op if it isn't a valid absolute URL.</summary>
    Task OpenAsync(string url);
}
