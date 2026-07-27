namespace KHost.Mobile.Services;

/// <summary>
/// Routes the Android hardware/gesture back button to dismiss an open in-page overlay (sheet, confirm pop-up, menu)
/// instead of minimizing the app. A host component creates one, supplying a callback that closes the single
/// top-most overlay it owns; the guard registers with <see cref="IBackButtonService"/> for the component's
/// lifetime and unregisters on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// The registry is consulted synchronously on the Android UI thread by the platform back-press integration (see the
/// Android <c>MainActivity</c>), so no browser-history manipulation is involved. An earlier scheme parked a marker
/// history entry and cancelled the pop with <c>PreventNavigation()</c>; the WebView coalesced the re-parked entry,
/// so a second back press on a nested overlay minimized the app instead of closing the next layer.
/// </remarks>
public sealed class BackButtonOverlayGuard : IDisposable
{
    private IDisposable? _registration;

    /// <param name="backButton">The app-wide back-press registry.</param>
    /// <param name="closeTopMost">Closes the single top-most overlay and returns true when it closed one; returns
    /// false when nothing is open (the guard consumes a back press only when this returns true).</param>
    /// <param name="notifyStateChanged">Re-renders the host after an overlay is closed (typically <c>StateHasChanged</c>).</param>
    public BackButtonOverlayGuard(
        IBackButtonService backButton,
        Func<bool> closeTopMost,
        Action notifyStateChanged)
    {
        ArgumentNullException.ThrowIfNull(backButton);
        ArgumentNullException.ThrowIfNull(closeTopMost);
        ArgumentNullException.ThrowIfNull(notifyStateChanged);

        // Registered for the whole component lifetime; the handler no-ops (returns false) when no overlay is open,
        // so it only consumes a back press when there's actually something to close. Runs on the UI thread.
        _registration = backButton.Register(() =>
        {
            if (!closeTopMost())
                return false;
            notifyStateChanged();
            return true;
        });
    }

    public void Dispose()
    {
        _registration?.Dispose();
        _registration = null;
    }
}
