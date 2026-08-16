namespace KHost.Mobile.UI.Components;

public sealed partial class SplitButtonItem
{
    // The owning SplitButton, so a selected item can dismiss the menu.
    [CascadingParameter] private SplitButton? Parent { get; set; }

    /// <summary>The row's label (and any inline markup).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Optional leading glyph/emoji shown before the label.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Optional muted second line under the label.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>When true, a divider is drawn above this row to group it apart from the ones before it.</summary>
    [Parameter] public bool Separated { get; set; }

    /// <summary>Disables this row (it stays visible but can't be tapped).</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Invoked when the row is tapped, before the menu closes.</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    private async Task ClickAsync()
    {
        if (Disabled)
            return;
        await OnClick.InvokeAsync();
        Parent?.CloseFromItem();
    }
}
