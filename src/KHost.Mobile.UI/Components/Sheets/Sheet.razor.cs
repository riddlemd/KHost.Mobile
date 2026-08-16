namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class Sheet : IDisposable
{
    /// <summary>Whether the sheet is showing. Defaults to true so a sheet already guarded by the host's own
    /// <c>@if</c> can just be wrapped — needed because Razor rejects <c>@{ }</c> inside child content.</summary>
    [Parameter] public bool Open { get; set; } = true;

    /// <summary>Extra classes on the panel, for a variant (e.g. <c>filter-sheet</c>). Stacking is automatic — see khSheet.restack.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra classes on the backdrop. Not needed for stacking; khSheet.restack handles that.</summary>
    [Parameter] public string? BackdropClass { get; set; }

    /// <summary>Accessible name for the dialog.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Whether to render the ✕. Off for sheets that provide their own dismiss controls.</summary>
    [Parameter] public bool ShowClose { get; set; } = true;

    /// <summary>Pull distance needed to dismiss. Raise it for a sheet with an inner scrolling list, so a
    /// scroll-flick can't close it by accident.</summary>
    [Parameter] public int? ClosePx { get; set; }

    /// <summary>Raised by the ✕ and the backdrop — and by a pull-down dismiss unless <see cref="OnSwipeDismiss"/>
    /// is wired.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Raised by a pull-down dismiss when it means something different from a plain close — the rating
    /// prompt logs the sing unrated rather than discarding it.</summary>
    [Parameter] public EventCallback OnSwipeDismiss { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private ElementReference _el;
    private DotNetObjectReference<Sheet>? _selfRef;
    private bool _bound;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The panel remounts on each open, so rebind then and drop the flag when it's gone.
        if (Open && !_bound)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSheet.register", _el, _selfRef,
                new { closeMethod = nameof(CloseFromSwipeAsync), closePx = ClosePx });
            _bound = true;
        }
        else if (!Open)
        {
            _bound = false;
        }

        await JS.InvokeVoidAsync("khSheet.syncLock");
    }

    private Task CloseAsync() => OnClose.InvokeAsync();

    // The swipe already animated the panel off-screen; just report it.
    [JSInvokable]
    public async Task CloseFromSwipeAsync()
    {
        await (OnSwipeDismiss.HasDelegate ? OnSwipeDismiss : OnClose).InvokeAsync();
        StateHasChanged();
    }

    public void Dispose()
    {
        _selfRef?.Dispose();
        // Torn down mid-open (a tab change with a sheet up) — the panel goes with it, so re-read the lock.
        _ = JS.InvokeVoidAsync("khSheet.syncLock");
    }
}
