namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class SongDetailSheet
{
    /// <summary>The song to show. Null hides the sheet; setting a different song reopens it in view mode.</summary>
    [Parameter] public SongListItem? Item { get; set; }

    /// <summary>True while a metadata lookup is in flight, so blank genre/year read as "…" rather than "—".</summary>
    [Parameter] public bool SuggestionLoading { get; set; }

    /// <summary>"We filled in X" note from the last auto-fill, shown under the fields. Null hides it.</summary>
    [Parameter] public string? AutoFilledNote { get; set; }

    /// <summary>Takes the song's <see cref="SongListItem.SuggestedTitle"/>/<see cref="SongListItem.SuggestedArtist"/>
    /// as the real spelling. Leave unwired to hide the hint entirely.</summary>
    [Parameter] public EventCallback OnApplySuggestion { get; set; }

    /// <summary>Rejects the suggested spelling for good.</summary>
    [Parameter] public EventCallback OnDismissSuggestion { get; set; }

    /// <summary>Whether the song is already on tonight's set — drives the Tonight button's label and the KaraFun split.</summary>
    [Parameter] public bool InTonight { get; set; }

    /// <summary>Whether to offer KaraFun: the host resolves the setting AND the active venue's KaraFun ID.</summary>
    [Parameter] public bool KaraFunAvailable { get; set; }

    /// <summary>Confidence-weighted star for "how it went"; null renders "not rated".</summary>
    [Parameter] public double? BayesScore { get; set; }

    /// <summary>Whole-star value the "how it went" rating draws, paired with <see cref="BayesScore"/>.</summary>
    [Parameter] public int RoundedAverage { get; set; }

    /// <summary>Existing tags across the list, offered as suggestions in the edit form.</summary>
    [Parameter] public IReadOnlyList<string> UsedTags { get; set; } = [];

    /// <summary>Whether to offer Edit; Tonight opens the card read-only.</summary>
    [Parameter] public bool AllowEdit { get; set; } = true;

    /// <summary>Raised on ✕, the backdrop, or a pull-down dismiss. The host clears <see cref="Item"/>.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Raised by "Log performance" / "Log another performance" — the host runs its rating prompt.</summary>
    [Parameter] public EventCallback OnLogPerformance { get; set; }

    /// <summary>Raised by the Tonight button; the host adds or removes the song.</summary>
    [Parameter] public EventCallback OnToggleTonight { get; set; }

    /// <summary>Raised by the "Last" date link; the host opens its history sheet.</summary>
    [Parameter] public EventCallback OnOpenHistory { get; set; }

    /// <summary>Quick-link taps. The host owns the URL building and the launch.</summary>
    [Parameter] public EventCallback OnOpenYouTube { get; set; }

    /// <inheritdoc cref="OnOpenYouTube" />
    [Parameter] public EventCallback OnOpenSpotify { get; set; }

    /// <inheritdoc cref="OnOpenYouTube" />
    [Parameter] public EventCallback OnOpenKaraFun { get; set; }

    /// <inheritdoc cref="OnOpenYouTube" />
    [Parameter] public EventCallback OnOpenLyrics { get; set; }

    /// <summary>The KaraFun split button's menu action: queue the song for tonight, then open KaraFun.</summary>
    [Parameter] public EventCallback OnAddToTonightAndOpenKaraFun { get; set; }

    /// <summary>New enjoyment rating; the host persists it.</summary>
    [Parameter] public EventCallback<int> OnEnjoymentChanged { get; set; }

    /// <summary>The edited values, on Save. The host applies them to the song and persists.</summary>
    [Parameter] public EventCallback<Edit> OnSave { get; set; }

    /// <summary>The edit form's values, handed to the host to apply. Tags are raw — the host normalizes.</summary>
    public sealed record Edit(string Title, string Artist, string Genre, int? Year, string Notes, List<string> Tags);

    private bool _editing;
    private string _editTitle = string.Empty;
    private string _editArtist = string.Empty;
    private string _editGenre = string.Empty;
    private string _editNotes = string.Empty;
    private int? _editYear;
    private List<string> _editTags = [];
    private Guid? _openFor;   // the song the sheet is currently showing; a change means a fresh open → leave edit mode

    private bool _editFieldsBound;

    private int CurrentYear => Clock.GetLocalNow().Year;
    private bool EditYearValid => !_editYear.HasValue || (_editYear.Value >= 1 && _editYear.Value <= CurrentYear);
    private bool CanSaveEdit => !string.IsNullOrWhiteSpace(_editTitle) && EditYearValid;

    // Shown only when the host wired both answers — the read-only Tonight host can't service either.
    private bool HasSuggestion =>
        Item is { HasSuggestion: true } && OnApplySuggestion.HasDelegate && OnDismissSuggestion.HasDelegate;

    private bool _suggestionOpen;

    protected override void OnParametersSet()
    {
        if (Item?.Id != _openFor)
        {
            _openFor = Item?.Id;
            _editing = false;
            _suggestionOpen = false;   // a different song's offer starts folded away
        }
        if (!HasSuggestion)
            _suggestionOpen = false;   // applied or dismissed: nothing left to show
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The edit form's year input needs the digit-only guard once it's rendered.
        if (_editing && !_editFieldsBound)
        {
            await JS.InvokeVoidAsync("khNumeric.register");
            _editFieldsBound = true;
        }
        else if (!_editing)
        {
            _editFieldsBound = false;
        }
    }

    private void StartEdit()
    {
        if (Item is null)
            return;
        _editTitle = Item.Title;
        _editArtist = Item.Artist;
        _editGenre = Item.Genre ?? string.Empty;
        _editNotes = Item.Notes ?? string.Empty;
        _editYear = Item.Year;
        _editTags = [.. Item.Tags];
        _editing = true;
    }

    private void CancelEdit() => _editing = false;

    private async Task SaveEditAsync()
    {
        if (Item is null || !CanSaveEdit)
            return;

        await OnSave.InvokeAsync(new Edit(_editTitle.Trim(), _editArtist.Trim(), _editGenre, _editYear, _editNotes, _editTags));
        _editing = false;
    }
}
