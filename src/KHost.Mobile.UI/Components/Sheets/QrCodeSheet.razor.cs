namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class QrCodeSheet
{
    /// <summary>Whether the sheet is showing.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>Heading text, e.g. the venue's name.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional line under the title.</summary>
    [Parameter] public string? Subtitle { get; set; }

    /// <summary>The QR code itself, as an <c>&lt;svg&gt;</c> element.</summary>
    [Parameter] public MarkupString Svg { get; set; }

    /// <summary>Optional text under the code — typically the URL it encodes, so it can be read out or typed.</summary>
    [Parameter] public string? Caption { get; set; }

    /// <summary>Accessible name for the sheet.</summary>
    [Parameter] public string AriaLabel { get; set; } = "QR code";

    /// <summary>Raised by the ✕, the backdrop and a pull-down dismiss.</summary>
    [Parameter] public EventCallback OnClose { get; set; }
}
