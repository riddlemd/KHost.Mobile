using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Layout;

public sealed partial class VenueChip : IDisposable
{
    private IReadOnlyList<Venue> _venues = [];
    private Venue? _active;
    private bool _open;
    private BackButtonOverlayGuard? _backGuard;
    private CancellationTokenSource? _trackCts;

    protected override async Task OnInitializedAsync()
    {
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseSwitcher,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnStoreChanged;
        Session.ActiveVenueChanged += OnActiveChanged;
        await ReloadAsync();
        StartLocationTracking();
    }

    private void StartLocationTracking()
    {
        if (!Settings.LocationAutoDetect)
            return;
        _trackCts = new CancellationTokenSource();
        _ = TrackLocationAsync(_trackCts.Token);
    }

    private async Task TrackLocationAsync(CancellationToken ct)
    {
        try
        {
            await Locator.ResolveActiveAsync(ct);
            var minutes = Math.Clamp(Settings.VenueRecheckMinutes, 2, 30);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            while (await timer.WaitForNextTickAsync(ct))
                await Locator.ResolveActiveAsync(ct);
        }
        catch (OperationCanceledException) { /* stopped on dispose */ }
        catch (Exception ex)
        {
            // best-effort background tracking — a missed re-check is harmless
            Log.LogWarning(ex, "Venue re-check failed");
        }
    }

    private bool IsActive(Venue v) => Session.ActiveVenueId == v.Id;

    // The switcher's quick list only. _venues stays the full set (so an active hidden venue is still resolved for
    // the chip, and management still sees everything); only the dropdown rows honor ShowInSwitcher.
    private IEnumerable<Venue> ListedVenues => _venues.Where(v => v.ShowInSwitcher);

    // VenueLocator short-circuits on this same check, so without it Auto/Pinned would be a choice with no
    // effect. Counts hidden venues too — they're still matched on proximity.
    private bool AnyGeolocatedVenue => _venues.Any(v => v.HasLocation);

    private void OnStoreChanged(object? sender, EventArgs e) => _ = ReloadOnUiAsync();
    private void OnActiveChanged(object? sender, EventArgs e) => _ = ReloadOnUiAsync();

    private async Task ReloadOnUiAsync()
    {
        try { await InvokeAsync(ReloadAsync); }
        catch (Exception ex)
        {
            // best-effort: a stale chip is harmless
            Log.LogWarning(ex, "Venue chip refresh failed");
        }
    }

    private async Task ReloadAsync()
    {
        _venues = await Store.GetAllAsync();
        _active = _venues.FirstOrDefault(v => v.Id == Session.ActiveVenueId);
        StateHasChanged();
    }

    private void Toggle() => _open = !_open;
    private void Close() => _open = false;

    private bool TryCloseSwitcher()
    {
        if (_open) { Close(); return true; }
        return false;
    }

    // A manual pick pins the venue so the periodic geo re-check won't stomp it (until "resume auto-detect").
    private void Activate(Venue v)
    {
        Session.SetActiveVenue(v.Id, pinned: true);
        Close();
    }

    private void ClearActive()
    {
        Session.SetActiveVenue(null, pinned: true);   // "not at a venue" is a deliberate choice — pin it too
        Close();
    }

    // Resolve immediately, not just on the next timer tick, or the mode flip appears to do nothing.
    // The switcher is left open so the flip stays visible.
    private async Task SetAutoAsync()
    {
        Session.SetActiveVenue(Session.ActiveVenueId, pinned: false);
        try { await Locator.ResolveActiveAsync(); }
        catch (Exception ex)
        {
            // best-effort
            Log.LogWarning(ex, "Active-venue resolve failed");
        }
    }

    // 📌 Pinned so the periodic geo re-check leaves it alone.
    private void SetPinned() => Session.SetActiveVenue(Session.ActiveVenueId, pinned: true);

    private void ManageVenues()
    {
        Close();
        Nav.NavigateTo("venues");
    }

    public void Dispose()
    {
        _backGuard?.Dispose();
        _trackCts?.Cancel();
        _trackCts?.Dispose();
        Store.Changed -= OnStoreChanged;
        Session.ActiveVenueChanged -= OnActiveChanged;
    }
}
