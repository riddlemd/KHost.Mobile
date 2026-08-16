using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Pages;

public sealed partial class Singers : IDisposable
{
    private IReadOnlyList<Singer> _singers = [];
    private Singer? _editing;
    private bool _editingIsNew;
    private BackButtonOverlayGuard? _backGuard;
    private ElementReference _gestureRoot;
    private DotNetObjectReference<Singers>? _selfRef;
    private bool _gesturesBound;

    protected override async Task OnInitializedAsync()
    {
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseTopOverlay,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnChanged;
        Session.ActiveSingerChanged += OnChanged;
        await ReloadAsync();

        // Deep-link from the switcher's "＋ Add" (singers?add=1) opens the add sheet straight away.
        if (new Uri(Nav.Uri).Query.Contains("add=1", StringComparison.Ordinal))
            OpenAdd();
    }

    private bool IsActive(Singer s) => Session.ActiveSingerId == s.Id;

    private void OnChanged(object? sender, EventArgs e) => _ = ReloadOnUiAsync();

    private async Task ReloadOnUiAsync()
    {
        try { await InvokeAsync(ReloadAsync); }
        catch (Exception ex)
        {
            // best-effort refresh
            Log.LogWarning(ex, "Singers reload after a store change failed");
        }
    }

    private async Task ReloadAsync()
    {
        _singers = await Store.GetAllAsync();
        StateHasChanged();
    }

    private void SetActive(Singer s)
    {
        Session.SetActiveSinger(s.Id);
        Settings.LastActiveSingerId = s.Id.ToString();   // remember for next launch
    }

    // Row gestures live in JS so a scroll or a stray drag can't fire them.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_singers.Count > 0 && !_gesturesBound)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSwipe.register", _gestureRoot, _selfRef,
                new { idAttr = "data-singer-id", tapMethod = nameof(OpenEditByIdAsync), holdMethod = nameof(SetActiveByIdAsync), swipeEnabled = false });
            _gesturesBound = true;
        }
    }

    // Tap (a stationary pointerup) from swipe.js. Called from JS, so render explicitly.
    [JSInvokable]
    public Task OpenEditByIdAsync(string id)
    {
        if (Guid.TryParse(id, out var guid))
        {
            var s = _singers.FirstOrDefault(x => x.Id == guid);
            if (s is not null)
            {
                OpenEdit(s);
                StateHasChanged();
            }
        }
        return Task.CompletedTask;
    }

    // Press-and-hold from swipe.js switches singer — the haptic tick is what confirms the hold took, since the
    // row's re-render is the only other feedback.
    [JSInvokable]
    public Task SetActiveByIdAsync(string id)
    {
        if (Guid.TryParse(id, out var guid))
        {
            var s = _singers.FirstOrDefault(x => x.Id == guid);
            if (s is not null && !IsActive(s))
            {
                SetActive(s);
                Haptics.LongPress();
                StateHasChanged();
            }
        }
        return Task.CompletedTask;
    }

    // ---- Add / edit ----
    private void OpenAdd()
    {
        _editing = new Singer { Color = SingerColors.Default };
        _editingIsNew = true;
    }

    // Edit a COPY so Cancel leaves the stored singer untouched; the store replaces by id on save.
    private void OpenEdit(Singer s)
    {
        _editing = Clone(s);
        _editingIsNew = false;
    }

    private void CloseEdit() => _editing = null;

    // Switching from the sheet closes it: the point of the switch is to get back to that singer's lists.
    private void SetActiveFromSheet(Singer s)
    {
        SetActive(s);
        _editing = null;
    }

    private async Task SaveAsync(Singer s)
    {
        if (_editingIsNew)
            await Store.AddAsync(s);
        else
            await Store.UpdateAsync(s);
        _editing = null;
    }

    private async Task DeleteAsync(Singer s)
    {
        _editing = null;
        if (_singers.Count <= 1)
            return;   // never remove the last singer

        // Deleting the active singer: hand active off to another first, so the app never points at a gone singer
        // (and the store drops the deleted singer's files cleanly).
        if (Session.ActiveSingerId == s.Id)
            SetActive(_singers.First(x => x.Id != s.Id));

        await Store.RemoveAsync(s.Id);
        Session.ClearMySongsView(s.Id);   // their per-singer view state has nothing to belong to now
    }

    private bool TryCloseTopOverlay()
    {
        if (_editing is not null) { CloseEdit(); return true; }
        return false;
    }

    private static Singer Clone(Singer s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Color = s.Color,
        Glyph = s.Glyph,
        Order = s.Order,
        AddedAt = s.AddedAt,
    };

    public void Dispose()
    {
        _backGuard?.Dispose();
        _selfRef?.Dispose();
        Store.Changed -= OnChanged;
        Session.ActiveSingerChanged -= OnChanged;
    }
}
