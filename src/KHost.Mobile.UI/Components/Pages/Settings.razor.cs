using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Pages;

public sealed partial class Settings : IDisposable
{
    // The clear buttons are gated on / labeled with their live counts, so track both and refresh on any change.
    private int _songCount;
    private int _lyricsCacheCount;
    private int _albumArtCount;
    private bool _confirmClear;

    // Stored as an int so a future "eager" tier needs no migration; until that exists it's a plain on/off switch.
    private bool SpellingAssistantEnabled
    {
        get => Prefs.SpellingSuggestionLevel > 0;
        set => Prefs.SpellingSuggestionLevel = value ? 1 : 0;
    }

    // Radii are stored in metres; the feet labels are the round numbers a US singer expects, not exact conversions
    // (250 ft is 76.2 m), so the pairs are spelled out rather than computed.
    private static readonly (int Metres, string Feet)[] VenueRadiusChoices =
    [
        (15, "50 ft"), (30, "100 ft"), (75, "250 ft"), (150, "500 ft"), (300, "1000 ft"),
    ];

    // Collapsible sections: default to all expanded, so nothing is hidden on first visit.
    private readonly HashSet<string> _collapsed = [];
    private bool IsOpen(string id) => !_collapsed.Contains(id);
    private void Toggle(string id) { if (!_collapsed.Add(id)) _collapsed.Remove(id); }

    protected override async Task OnInitializedAsync()
    {
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseTopOverlay,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnStoreChanged;
        LyricsCache.Changed += OnStoreChanged;
        AlbumArt.Changed += OnStoreChanged;
        await RefreshCountAsync();
    }

    // Routes the Android back button to close the top-most overlay instead of navigating; active only while one is open.
    private BackButtonOverlayGuard? _backGuard;

    // Closes the confirm-clear pop-up — the only overlay this page has.
    private bool TryCloseTopOverlay()
    {
        if (_confirmClear) { CancelClear(); return true; }
        return false;
    }

    // Same non-async-void bridge the other pages use: marshal the reload onto the render thread and swallow failures.
    private void OnStoreChanged(object? sender, EventArgs e) => _ = RefreshFromStoreAsync();

    private async Task RefreshFromStoreAsync()
    {
        try { await InvokeAsync(RefreshCountAsync); }
        catch (Exception ex)
        {
            // best-effort: a failed count refresh just leaves the last-known value
            Log.LogWarning(ex, "Settings count refresh failed");
        }
    }

    private async Task RefreshCountAsync()
    {
        _songCount = (await Store.GetAllAsync()).Count;
        _lyricsCacheCount = await LyricsCache.CountAsync();
        _albumArtCount = await AlbumArt.CountAsync();
        StateHasChanged();
    }

    // Cached lyrics are recoverable (they re-fetch on next open), so this clears straight away — no confirm sheet.
    private async Task ClearLyricsCacheAsync()
    {
        await LyricsCache.ClearAsync();   // LyricsCache.Changed → RefreshCountAsync updates the count + button
    }

    // Cached covers re-download on next view, so this also clears straight away — no confirm sheet.
    private async Task ClearAlbumArtCacheAsync()
    {
        await AlbumArt.ClearAsync();   // AlbumArt.Changed → RefreshCountAsync updates the count + button
        // The images already handed to the WebView are a separate copy: without this, deleting the files leaves
        // every visible card still showing its cover, so the button looks like it did nothing.
        await AlbumArtService.ClearAsync();
    }

    private void AskClear()
    {
        if (_songCount > 0)
            _confirmClear = true;
    }

    private void CancelClear() => _confirmClear = false;

    // TutorialOverlay picks up the navigation to "/" via LocationChanged and kicks off the tour.
    private void ReplayTutorial()
    {
        Prefs.TutorialCompleted = false;
        Session.TutorialResolved = false;
        Nav.NavigateTo("/");
    }

    private async Task ConfirmClearAsync()
    {
        _confirmClear = false;
        await Store.ClearAsync();   // Store.Changed → RefreshCountAsync updates the count + button
    }

    public void Dispose()
    {
        _backGuard?.Dispose();
        Store.Changed -= OnStoreChanged;
        LyricsCache.Changed -= OnStoreChanged;
        AlbumArt.Changed -= OnStoreChanged;
    }
}
