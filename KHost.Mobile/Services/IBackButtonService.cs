namespace KHost.Mobile.Services;

/// <summary>
/// App-wide registry that lets the Android hardware/gesture back button dismiss an open in-page overlay
/// (sheet, confirm pop-up, menu) instead of minimizing the app. Components register a close-callback while
/// they are mounted (via <see cref="BackButtonOverlayGuard"/>); the Android platform layer consults this
/// on each back press. A no-op on platforms without a hardware back button (iOS) — the service still exists
/// so the shared components stay platform-agnostic, but nothing calls <see cref="HandleBack"/> there.
/// </summary>
public interface IBackButtonService
{
    /// <summary>
    /// Registers a handler that closes the single top-most overlay it owns and returns <c>true</c> when it
    /// actually closed one (and <c>false</c> when it had nothing open). Dispose the returned token to
    /// unregister. Handlers are consulted most-recently-registered first, so a page's overlay outranks the
    /// layout's ⋮ menu.
    /// </summary>
    IDisposable Register(Func<bool> closeTopMost);

    /// <summary>
    /// Invoked by the platform back-press integration. Runs the registered handlers newest-first and returns
    /// <c>true</c> as soon as one consumes the press (closed an overlay); returns <c>false</c> if none did, so
    /// the caller can fall back to the platform's default back behavior.
    /// </summary>
    bool HandleBack();
}
