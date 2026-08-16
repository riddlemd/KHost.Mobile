namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class RollResultSheet
{
    /// <summary>The picked song; null closes the sheet.</summary>
    [Parameter] public SongListItem? Item { get; set; }

    /// <summary>Whether the pick is already in tonight's set — the primary action then reads as done.</summary>
    [Parameter] public bool InTonight { get; set; }

    /// <summary>Whether the Tonight feature is on; with it off there's no primary action to offer.</summary>
    [Parameter] public bool ShowTonight { get; set; } = true;

    /// <summary>Confidence-weighted how-it-went star, or null when the song has never been rated.</summary>
    [Parameter] public double? BayesScore { get; set; }

    /// <summary>Rounded how-it-went average, for the filled star count.</summary>
    [Parameter] public int RoundedAverage { get; set; }

    /// <summary>Adds the pick to tonight's set.</summary>
    [Parameter] public EventCallback OnAddToTonight { get; set; }

    /// <summary>Opens the pick's detail sheet.</summary>
    [Parameter] public EventCallback OnOpen { get; set; }

    /// <summary>Draws again, leaving the sheet open.</summary>
    [Parameter] public EventCallback OnReroll { get; set; }

    /// <summary>Opens the picker's options.</summary>
    [Parameter] public EventCallback OnOptions { get; set; }

    /// <summary>Raised by the ✕, the backdrop and a pull-down dismiss.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    // The card is a div playing the part of a button, so it has to answer the keys a real one would.
    private Task OpenOnEnterOrSpaceAsync(KeyboardEventArgs e)
        => e.Key is "Enter" or " " ? OnOpen.InvokeAsync() : Task.CompletedTask;
}
