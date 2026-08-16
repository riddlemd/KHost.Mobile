namespace KHost.Mobile.UI.Components;

public sealed partial class SplitButton : IDisposable
{
    /// <summary>The primary (default) action's label. Ignored when <see cref="PrimaryContent"/> is set.</summary>
    [Parameter] public string? PrimaryLabel { get; set; }

    /// <summary>Optional rich content for the primary segment (icon + label markup); overrides <see cref="PrimaryLabel"/>.</summary>
    [Parameter] public RenderFragment? PrimaryContent { get; set; }

    /// <summary>Invoked when the primary segment is tapped — the button's default action.</summary>
    [Parameter] public EventCallback OnPrimary { get; set; }

    /// <summary>The dropdown's rows — a sequence of <see cref="SplitButtonItem"/> elements.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Which way the menu opens: <see cref="SplitDirection.Down"/> (default) or <see cref="SplitDirection.Up"/>
    /// for a button low in a sheet.</summary>
    [Parameter] public SplitDirection Direction { get; set; } = SplitDirection.Down;

    /// <summary>Which edge the menu aligns to: <see cref="SplitMenuAlign.End"/> (default, right) or Start (left).</summary>
    [Parameter] public SplitMenuAlign Align { get; set; } = SplitMenuAlign.End;

    /// <summary>The fill for both segments — Primary (default), Tonal, or Secondary.</summary>
    [Parameter] public SplitVariant Variant { get; set; } = SplitVariant.Primary;

    /// <summary>Disables both segments.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Accessible label for the chevron toggle (the visible glyph is decorative).</summary>
    [Parameter] public string ToggleAriaLabel { get; set; } = "More options";

    /// <summary>Extra CSS classes appended to the container — e.g. a brand/layout modifier like <c>kh-split--karafun</c>.</summary>
    [Parameter] public string? Class { get; set; }

    private bool _open;
    private BackButtonOverlayGuard? _backGuard;

    // Registered for the component's lifetime, not per open: the guard no-ops when the menu is closed, so it only
    // consumes a back press when there's something to dismiss.
    protected override void OnInitialized() =>
        _backGuard = new BackButtonOverlayGuard(BackButton, TryClose, StateHasChanged);

    private bool TryClose()
    {
        if (!_open)
            return false;
        _open = false;
        return true;
    }

    private void Toggle() => _open = !_open;

    private void Close() => _open = false;

    private async Task PrimaryClickAsync()
    {
        _open = false;
        await OnPrimary.InvokeAsync();
    }

    // Called by a child SplitButtonItem after its own click runs — this component owns the open/close state.
    internal void CloseFromItem()
    {
        _open = false;
        StateHasChanged();
    }

    private string VariantBtnClass => Variant switch
    {
        SplitVariant.Tonal => "btn-tonal",
        SplitVariant.Secondary => "btn-secondary",
        _ => "btn-primary",
    };

    private string DirectionAttr => Direction == SplitDirection.Up ? "up" : "down";
    private string AlignAttr => Align == SplitMenuAlign.Start ? "start" : "end";

    public void Dispose() => _backGuard?.Dispose();
}
