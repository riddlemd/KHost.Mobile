namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class SurpriseSheet
{
    /// <summary>Whether the sheet is showing.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>Raised by the roll button — the host closes the sheet and picks.</summary>
    [Parameter] public EventCallback OnRoll { get; set; }

    /// <summary>Raised by the ✕, the backdrop and a pull-down dismiss.</summary>
    [Parameter] public EventCallback OnClose { get; set; }
}
