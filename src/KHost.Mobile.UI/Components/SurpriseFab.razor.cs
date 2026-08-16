namespace KHost.Mobile.UI.Components;

public sealed partial class SurpriseFab : IDisposable
{
    /// <summary>Drops the button to sit above the safe area instead of the bottom tab bar, matching the "+".</summary>
    [Parameter] public bool NoNav { get; set; }

    /// <summary>Tucks the button off-screen — set while a sheet, overlay or snackbar is showing.</summary>
    [Parameter] public bool Hidden { get; set; }

    /// <summary>Raised by a tap: roll now, using the saved options.</summary>
    [Parameter] public EventCallback OnRoll { get; set; }

    /// <summary>Raised by a press-and-hold: open the options instead of rolling.</summary>
    [Parameter] public EventCallback OnOptions { get; set; }

    private ElementReference _el;
    private DotNetObjectReference<SurpriseFab>? _selfRef;
    private bool _bound;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_bound) return;
        _selfRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("khPressHold.register", _el, _selfRef,
            new { tapMethod = nameof(TapAsync), holdMethod = nameof(HoldAsync) });
        _bound = true;
    }

    [JSInvokable]
    public Task TapAsync() => OnRoll.InvokeAsync();

    /// <summary>Confirms the hold with a haptic tick — the only cue that a hold registered.</summary>
    [JSInvokable]
    public Task HoldAsync()
    {
        Haptics.LongPress();
        return OnOptions.InvokeAsync();
    }

    public void Dispose() => _selfRef?.Dispose();
}
