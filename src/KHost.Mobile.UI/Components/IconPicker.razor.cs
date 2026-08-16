namespace KHost.Mobile.UI.Components;

public sealed partial class IconPicker
{
    /// <summary>The selected glyph. When <see cref="LetterFallback"/> is set, an empty string means "use the letter".</summary>
    [Parameter] public string Glyph { get; set; } = string.Empty;

    /// <summary>Raised with the new glyph when a glyph (or the letter tile) is picked.</summary>
    [Parameter] public EventCallback<string> GlyphChanged { get; set; }

    /// <summary>The pickable emoji/glyph set.</summary>
    [Parameter] public IReadOnlyList<string> AvailableGlyphs { get; set; } = [];

    /// <summary>The selected color (hex), or null for a glyph-only picker (renders a surface tile, no color row).</summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>Raised with the new color when a color is picked (only when <see cref="AvailableColors"/> is supplied).</summary>
    [Parameter] public EventCallback<string> ColorChanged { get; set; }

    /// <summary>The color palette; null/empty hides the color row (and renders a tile preview instead of an avatar).</summary>
    [Parameter] public IReadOnlyList<string>? AvailableColors { get; set; }

    /// <summary>When non-null, a "use the first letter" option is offered (clearing the glyph) and the preview shows
    /// this letter whenever the glyph is empty. Pass the name's initial; leave null for a glyph-always picker.</summary>
    [Parameter] public string? LetterFallback { get; set; }

    /// <summary>Content placed beside the preview on its row — typically the name input.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool _open;

    private bool HasColor => !string.IsNullOrWhiteSpace(Color);
    private bool HasColorPalette => AvailableColors is { Count: > 0 };

    private string Preview => !string.IsNullOrWhiteSpace(Glyph) ? Glyph : (LetterFallback ?? "?");

    private void Toggle() => _open = !_open;

    // Glyph is the primary choice, so picking one closes the panel; picking a color (below) leaves it open so a
    // glyph can still be chosen in the same pass.
    private async Task SelectGlyphAsync(string g)
    {
        Glyph = g;
        await GlyphChanged.InvokeAsync(g);
        _open = false;
    }

    private Task SelectColorAsync(string c)
    {
        Color = c;
        return ColorChanged.InvokeAsync(c);
    }
}
