namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class SingerEditSheet
{
    /// <summary>The singer being added/edited. Null hides the sheet. The host passes a fresh <see cref="Singer"/> to
    /// add or a working copy to edit; on save its fields are written and it's handed back via <see cref="OnSave"/>.</summary>
    [Parameter] public Singer? Editing { get; set; }

    /// <summary>Whether this is a brand-new singer (drives the title and hides delete).</summary>
    [Parameter] public bool IsNew { get; set; }

    /// <summary>Whether delete is allowed — false when this is the only singer, so the last one can't be removed.</summary>
    [Parameter] public bool CanDelete { get; set; }

    /// <summary>Raised with the populated singer when the user saves (name guaranteed non-blank).</summary>
    [Parameter] public EventCallback<Singer> OnSave { get; set; }

    /// <summary>Raised with the singer when the user confirms delete (only offered when <see cref="CanDelete"/>).</summary>
    [Parameter] public EventCallback<Singer> OnDelete { get; set; }

    /// <summary>Raised when the sheet is dismissed without saving (✕ / Cancel / backdrop / swipe).</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Whether the singer being edited is the active one — drives "Switch to this singer" vs the
    /// "currently singing" note.</summary>
    [Parameter] public bool IsActive { get; set; }

    /// <summary>Raised with the singer when the user switches to them from the sheet. This is the accessible
    /// counterpart to the list row's press-and-hold.</summary>
    [Parameter] public EventCallback<Singer> OnSetActive { get; set; }

    private string _name = string.Empty;
    private string _color = SingerColors.Default;
    private string _glyph = string.Empty;   // empty = use the first letter; otherwise the chosen emoji
    private Guid? _seededFor;   // the singer id the buffers belong to; a change means a fresh open → reseed

    private bool HasGlyph => !string.IsNullOrWhiteSpace(_glyph);

    // The letter shown on the avatar (and the picker's "use letter" tile) when no emoji is chosen.
    private string Initial =>
        string.IsNullOrWhiteSpace(_name) ? "?" : char.ToUpperInvariant(_name.TrimStart()[0]).ToString();

    // Clearing _seededFor on close matters as much as the id check: the component stays mounted while hidden, so
    // without it, reopening the SAME singer keeps the last open's buffers and abandoned edits look saved.
    protected override void OnParametersSet()
    {
        if (Editing is null)
        {
            _seededFor = null;
            return;
        }

        if (Editing is { } s && s.Id != _seededFor)
        {
            _seededFor = s.Id;
            _name = s.Name;
            _color = string.IsNullOrWhiteSpace(s.Color) ? SingerColors.Default : s.Color;
            _glyph = s.Glyph ?? string.Empty;
        }
    }

    private async Task SaveAsync()
    {
        if (Editing is not { } s || string.IsNullOrWhiteSpace(_name))
            return;

        s.Name = _name.Trim();
        s.Color = string.IsNullOrWhiteSpace(_color) ? SingerColors.Default : _color;
        s.Glyph = HasGlyph ? _glyph : null;
        await OnSave.InvokeAsync(s);
    }

    // Switching is a live action on the stored singer, not part of the edit buffer, so it doesn't wait for Save.
    private async Task ActivateAsync()
    {
        if (Editing is { } s)
            await OnSetActive.InvokeAsync(s);
    }

    private async Task DeleteAsync()
    {
        if (Editing is { } s)
            await OnDelete.InvokeAsync(s);
    }
}
