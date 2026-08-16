using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Layout;

public sealed partial class SingerChip : IDisposable
{
    private IReadOnlyList<Singer> _singers = [];
    private Singer? _active;
    private bool _open;
    private string? _appliedColor;   // the color last pushed to khSinger.apply, so we only re-tint on a real change
    private BackButtonOverlayGuard? _backGuard;

    protected override async Task OnInitializedAsync()
    {
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseSwitcher,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnChanged;
        Session.ActiveSingerChanged += OnChanged;
        await ReloadAsync();
    }

    private bool IsActive(Singer s) => Session.ActiveSingerId == s.Id;

    private void OnChanged(object? sender, EventArgs e) => _ = ReloadOnUiAsync();

    private async Task ReloadOnUiAsync()
    {
        try { await InvokeAsync(ReloadAsync); }
        catch (Exception ex)
        {
            // best-effort: a stale avatar is harmless
            Log.LogWarning(ex, "Singer chip refresh failed");
        }
    }

    private async Task ReloadAsync()
    {
        _singers = await Store.GetAllAsync();
        _active = _singers.FirstOrDefault(s => s.Id == Session.ActiveSingerId);
        StateHasChanged();
    }

    // Guarded on _appliedColor so a re-render doesn't re-invoke interop when the color hasn't actually changed.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var color = _active?.Color;
        if (color != _appliedColor)
        {
            _appliedColor = color;
            try { await JS.InvokeVoidAsync("khSinger.apply", color); }
            catch (Exception ex)
            {
                // best-effort: the un-tinted brand color is a fine fallback
                Log.LogWarning(ex, "Applying the singer tint failed; keeping the brand color");
            }
        }
    }

    private void Toggle() => _open = !_open;
    private void Close() => _open = false;

    private bool TryCloseSwitcher()
    {
        if (_open) { Close(); return true; }
        return false;
    }

    private void Activate(Singer s)
    {
        Session.SetActiveSinger(s.Id);
        Settings.LastActiveSingerId = s.Id.ToString();   // remember for next launch
        Close();
    }

    private void ManageSingers()
    {
        Close();
        Nav.NavigateTo("singers");
    }

    private static string AvatarStyle(Singer? s) => s is null ? "" : $"background:{s.Color};";

    public void Dispose()
    {
        _backGuard?.Dispose();
        Store.Changed -= OnChanged;
        Session.ActiveSingerChanged -= OnChanged;
    }
}
