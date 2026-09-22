using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Layout;

public sealed partial class MainLayout : IDisposable
{
    private string _theme = "light";
    private bool _menuOpen;
    private bool _ready;   // gates the shell until the singer bootstrap has run (see OnInitializedAsync)

    protected override void OnInitialized()
    {
        Nav.LocationChanged += OnLocationChanged;
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseTopOverlay,
            notifyStateChanged: StateHasChanged);
    }

    // Singer bootstrap: EnsureSeededAsync seeds the default singer on a fresh install
    // on first run. The active singer must be set before the shell (and its personal pages) render.
    protected override async Task OnInitializedAsync()
    {
        await Singers.EnsureSeededAsync();
        var all = await Singers.GetAllAsync();

        var active = Guid.TryParse(Settings.LastActiveSingerId, out var last)
            ? all.FirstOrDefault(s => s.Id == last)
            : null;
        active ??= all[0];   // EnsureSeededAsync guarantees at least one

        Session.SetActiveSinger(active.Id);
        Settings.LastActiveSingerId = active.Id.ToString();

        await RestoreActiveVenueAsync();
        _ready = true;
    }

    // The venue pointer and its pin live in the session, which dies with the process — and Android kills a
    // backgrounded app freely. Without this, every relaunch came back unpinned and let auto-detect re-pick.
    // Runs before _ready because VenueChip (gated on it) starts location tracking the moment it initializes.
    private async Task RestoreActiveVenueAsync()
    {
        var pinned = Settings.LastActiveVenuePinned;

        if (!Guid.TryParse(Settings.LastActiveVenueId, out var last))
        {
            // Empty id + pinned is a deliberate "not at a venue"; empty + unpinned is just a fresh install.
            if (pinned)
                Session.SetActiveVenue(null, pinned: true);
            return;
        }

        // A venue deleted since last launch must not leave the session pinned to nothing — drop it and let
        // auto-detect have another go.
        var venue = await Venues.GetAsync(last);
        Session.SetActiveVenue(venue?.Id, pinned: venue is not null && pinned);
    }

    // IAppSettings has no Changed event and the layout persists across pages, so a navigation is the only cue to
    // re-read TonightEnabled after it's toggled on the Settings page.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Earliest reliable moment to forward the native safe-area insets: the native side measures long before
            // the DOM exists to receive them. Where nothing was measured the values stay 0 and CSS max() picks env().
            Insets.Changed += OnInsetsChanged;
            await PushInsetsAsync();

            // Read whatever the head script / OS preference resolved to, so the icon matches.
            _theme = await JS.InvokeAsync<string>("khTheme.current");
            StateHasChanged();
        }
    }

    private Task PushInsetsAsync() => JS.InvokeVoidAsync("khInsets.set", Insets.Top, Insets.Bottom).AsTask();

    private void OnInsetsChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(async () =>
        {
            try { await PushInsetsAsync(); }
            catch (Exception ex)
            {
                // a push racing teardown is harmless — the next render pushes again
                Log.LogWarning(ex, "Safe-area inset push failed");
            }
        });

    // Routes the Android back button to close the ⋮ menu instead of navigating; active only while it's open.
    private BackButtonOverlayGuard? _backGuard;

    private bool TryCloseTopOverlay()
    {
        if (_menuOpen) { CloseMenu(); return true; }
        return false;
    }

    private void ToggleMenu() => _menuOpen = !_menuOpen;

    private void CloseMenu() => _menuOpen = false;

    // The menu stays open so the label flips and the change is visible.
    private async Task ToggleThemeAsync()
    {
        _theme = await JS.InvokeAsync<string>("khTheme.toggle");
    }

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        Insets.Changed -= OnInsetsChanged;
        _backGuard?.Dispose();
    }
}
