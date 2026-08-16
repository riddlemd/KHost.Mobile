namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class RatingPromptSheet
{
    /// <summary>The song a performance is being logged for. Null hides the sheet; setting it (re)opens the prompt reset to blank.</summary>
    [Parameter] public SongListItem? Item { get; set; }

    /// <summary>When true, show the "how it went" stars + Skip; when false, the prompt only collects an optional note.</summary>
    [Parameter] public bool RatePerformances { get; set; }

    /// <summary>Raised when the sing is logged — Save (with the rating), Skip, or a pull-down dismiss (both → 0).
    /// Carries the chosen how-it-went (0 when unrated/skipped), the optional note, and when it was sung.</summary>
    [Parameter] public EventCallback<(int howItWent, string? note, DateTimeOffset when)> OnCommit { get; set; }

    /// <summary>Raised when the prompt is dismissed WITHOUT logging (the ✕ or the backdrop).</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private int _value;
    private string _note = string.Empty;
    private string _when = string.Empty;
    private Guid? _openFor;   // the item id the current buffers belong to; a change means a fresh open → reset

    private string Hint => _value switch
    {
        1 => "Not confident",
        2 => "Shaky",
        3 => "Okay",
        4 => "Confident",
        5 => "Nailed it",
        _ => "Tap a star to rate — or skip below",
    };

    protected override void OnParametersSet()
    {
        if (Item?.Id != _openFor)
        {
            _openFor = Item?.Id;
            _value = 0;
            _note = string.Empty;
            _when = DateInput.Format(Clock.GetLocalNow());
        }
    }

    private void OnWhenChanged(ChangeEventArgs e) => _when = e.Value?.ToString() ?? string.Empty;

    // A cleared or unparseable field falls back to now, so a commit never fails on the date.
    private DateTimeOffset When =>
        DateInput.TryParse(_when, out var when) ? when : Clock.GetLocalNow();

    private Task SaveAsync() => OnCommit.InvokeAsync((_value, _note, When));

    private Task SkipAsync() => OnCommit.InvokeAsync((0, _note, When));

    private Task CancelAsync() => OnCancel.InvokeAsync();

    private Task DismissFromSwipeAsync() => OnCommit.InvokeAsync((0, _note, When));
}
