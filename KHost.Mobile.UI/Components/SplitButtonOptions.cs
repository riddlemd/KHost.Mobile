namespace KHost.Mobile.Components;

/// <summary>Which way a <see cref="SplitButton"/>'s dropdown opens.</summary>
public enum SplitDirection
{
    /// <summary>Menu drops below the button (default; for buttons near the top of the screen).</summary>
    Down,

    /// <summary>Menu opens above the button (for buttons low in a sheet or above the tab bar, so it stays on-screen).</summary>
    Up,
}

/// <summary>Which horizontal edge a <see cref="SplitButton"/>'s dropdown aligns to.</summary>
public enum SplitMenuAlign
{
    /// <summary>Align the menu to the button's left edge.</summary>
    Start,

    /// <summary>Align the menu to the button's right edge (default; matches the header ⋮ menu).</summary>
    End,
}

/// <summary>The button fill used for both segments of a <see cref="SplitButton"/>, mapping to the app's <c>.btn</c> variants.</summary>
public enum SplitVariant
{
    /// <summary>Solid accent fill (<c>.btn-primary</c>) — the default, for a page's main action.</summary>
    Primary,

    /// <summary>Soft accent fill (<c>.btn-tonal</c>) — a lighter emphasis.</summary>
    Tonal,

    /// <summary>Neutral surface fill (<c>.btn-secondary</c>).</summary>
    Secondary,
}
