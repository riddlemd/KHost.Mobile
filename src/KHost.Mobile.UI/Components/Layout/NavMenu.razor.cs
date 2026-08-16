using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Layout;

public sealed partial class NavMenu : IDisposable
{
    private int _remaining;

    protected override async Task OnInitializedAsync()
    {
        Tonight.Changed += OnTonightChanged;
        await RefreshAsync();
    }

    private void OnTonightChanged(object? sender, EventArgs e) => _ = RefreshOnUiAsync();

    private async Task RefreshOnUiAsync()
    {
        try { await InvokeAsync(RefreshAsync); }
        catch (Exception ex)
        {
            // best-effort: a stale badge is harmless
            Log.LogWarning(ex, "Tonight badge refresh failed");
        }
    }

    private async Task RefreshAsync()
    {
        var set = await Tonight.GetAllAsync();
        _remaining = set.Count(e => !e.Completed);
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose() => Tonight.Changed -= OnTonightChanged;
}
