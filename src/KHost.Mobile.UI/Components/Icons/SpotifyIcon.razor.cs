namespace KHost.Mobile.UI.Components.Icons;

public sealed partial class SpotifyIcon
{
    /// <summary>Width/height in px.</summary>
    [Parameter] public int Size { get; set; } = 24;

    /// <summary>Fill color. Brand green by default; pass "#fff" for a white mark on a colored background.</summary>
    [Parameter] public string Fill { get; set; } = "#1ed760";
}
