using KHost.Mobile.Abstractions.Clients.CoverArt;
using KHost.Mobile.Abstractions.Clients.Lyrics;
using KHost.Mobile.Abstractions.Clients.Metadata;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Pages;

public sealed partial class MySongs : IDisposable
{
    private IReadOnlyList<SongListItem> _items = [];
    private bool _loading = true;   // true only during the initial device-storage load; drives the equalizer loader
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string _notes = string.Empty;
    private string _genre = string.Empty;
    private int? _year;
    private List<string> _tags = [];   // add-form tags buffer; normalized onto the song when it's added

    // How many tag chips a card shows inline before collapsing the rest into a "+N" count.
    private const int CardTagLimit = 3;

    // Filters. Genre / rating / enjoyment are multi-select: an empty set = no filter, otherwise the picks are OR'd.
    private string _filterSearch = string.Empty;   // one box, matched against title OR artist
    private readonly HashSet<string> _filterGenres = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _filterRatings = [];      // "how it went" rounded average(s): 0 Not rated, 1-5 stars
    private readonly HashSet<int> _filterEnjoyments = [];   // enjoyment rating(s): 0 Not rated, 1-5 stars
    private readonly HashSet<string> _filterTags = new(StringComparer.OrdinalIgnoreCase);
    private bool _tagFilterAll;   // Tags filter combine mode: false = match ANY selected (OR, default), true = ALL (AND)

    // The six rating choices shared by the "how it went" and "enjoyment" multi-selects.
    private static readonly (int val, string label)[] RatingOptions =
        [(0, "Not rated"), (1, "★"), (2, "★★"), (3, "★★★"), (4, "★★★★"), (5, "★★★★★")];
    private bool _filterSheetOpen;   // the advanced-filters bottom sheet (opened from the funnel); transient, not persisted
    private ElementReference _filterSheet;
    private bool _filterSheetBound;
    private bool _addOpen;   // the add-song sheet (opened by the floating "+"); transient, not persisted
    private ElementReference _addSheet;
    private bool _addSheetBound;

    // Year range slider. Handles are null until the user drags them, then they follow the selection;
    // the effective bounds are the min/max release year present across dated songs (recomputed on refresh).
    private int? _filterYearLo;
    private int? _filterYearHi;
    private int _yearMin;
    private int _yearMax;
    private int _yearDistinctCount;
    private IReadOnlyList<int> _libraryYears = [];   // distinct release years, ascending — the dropdowns' options

    // Appended, never reordered: the ordinal is what SaveViewState persists.
    private enum SortColumn { Added, Title, Artist, HowItWent, Enjoyment, LastPerformed }
    private SortColumn _sort = SortColumn.Title;
    private bool _sortDescending = false;   // default: A→Z by song title

    // Swipe-to-remove wiring: the JS module is delegated on the card-list container, not per card.
    private ElementReference _swipeRoot;
    private DotNetObjectReference<MySongs>? _swipeRef;
    private bool _listRendered;
    private bool _swipeBound;

    // Infinite scroll: render only _renderCount of the filtered/sorted list so a 500+ song list paints ~20 cards
    // instead of all of them. _lastListSig rewinds paging to page 1 whenever the filter/sort changes.
    private const int PageSize = 20;
    private int _renderCount = PageSize;
    private int _visibleCount;
    private string _lastListSig = "";
    private ElementReference _sentinel;
    private bool _infiniteBound;
    private bool _pagingReset;   // set when a filter/sort change rewound paging → OnAfterRender scrolls back to top

    // The memoized filter+sort result and what it was computed from (see the render block).
    private List<SongListItem>? _sortedCache;
    private IReadOnlyList<SongListItem>? _sortedFor;
    private string? _sortedSig;
    private int _itemsVersion;            // bumped on every RefreshAsync so caches can't survive a reload
    private string _artSurfaceSig = "";   // what the art observer was last wired against
    private string _artObservedSig = "";

    private PerformanceHistorySheet? _historySheetRef;
    private ElementReference _lyricsSheet;
    private bool _lyricsSheetBound;
    private bool _pageLocked;   // mirrors body.kh-sheet-open; reconciled from sheet state each render

    // Read here only to light up each card's "Add to tonight" state; the set itself is managed on the Tonight tab.
    private IReadOnlyList<TonightEntry> _tonight = [];

    // Lyrics lookup (LRCLIB) shown in its own sheet; fetched fresh each open.
    private SongListItem? _lyricsItem;
    private string? _lyricsText;
    private string? _lyricsError;
    private bool _lyricsLoading;
    private bool _lyricsInstrumental;
    private CancellationTokenSource? _lyricsCts;

    // Undo-after-swipe-remove: the last removed item + a timer to expire the offer.
    private int UndoWindowMs => Settings.UndoWindowSeconds * 1000;
    private SongListItem? _undoItem;
    private SongListItem? _confirmRemove;   // the song whose remove confirm is armed (ConfirmSongDelete only)
    private CancellationTokenSource? _undoCts;
    private bool _rangeBound;   // anti-cross clamp bound to the current year-slider inputs
    private string? _scrollToSongId;   // set on favorite-toggle; the next render scrolls this card into view

    // Filter + sort + paging live on IAppSession (one view-state per singer), and scroll on scroll.js keyed per
    // singer, so they survive a tab change AND a singer switch — each singer's My Songs keeps its own filters, sort,
    // paged height and scroll position. The page component is disposed on navigation and its own fields would
    // otherwise reset; a singer switch keeps the page mounted but is detected in RefreshAsync (see below).
    private Guid? _viewSinger;   // the singer whose view state is currently loaded into this page's fields
    private string ScrollKey => ScrollKeyFor(_viewSinger);
    private static string ScrollKeyFor(Guid? singerId) => singerId is { } id ? $"mysongs:{id:N}" : "mysongs";
    private bool _scrollRestored;

    // The song whose detail card is open; the card itself is SongDetailSheet, but every store write behind it
    // stays here.
    private SongListItem? _detailItem;
    private bool _addFieldsBound;    // khNumeric bound to the add-form year input (re-bind when the form reopens)

    // Metadata auto-fill runs in the background on add, and again when the detail sheet opens; the spinner + note
    // below are sheet-only UI.
    private bool _suggestionLoading;
    private string? _autoFilledNote;
    private CancellationTokenSource? _suggestCts;                // detail-sheet lookup; cancelled when the sheet closes
    private readonly CancellationTokenSource _bgFillCts = new(); // add-time background lookups; cancelled on dispose

    // A release year is optional, but when supplied it must be a real year: 1..this year.
    private int CurrentYear => Clock.GetLocalNow().Year;
    private bool YearValid => !_year.HasValue || (_year.Value >= 1 && _year.Value <= CurrentYear);

    private bool CanAdd => !string.IsNullOrWhiteSpace(_title) && YearValid;

    // Non-blocking — the first Add on a match arms an "Add anyway" confirm; editing a field disarms it.
    private bool _confirmedDuplicate;

    private bool IsDuplicate =>
        !string.IsNullOrWhiteSpace(_title) &&
        _items.Any(i =>
            string.Equals(i.Title.Trim(), _title.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.Artist.Trim(), _artist.Trim(), StringComparison.OrdinalIgnoreCase));

    private string DuplicateMessage =>
        string.IsNullOrWhiteSpace(_artist)
            ? $"You already have \"{_title.Trim()}\" in your list."
            : $"You already have \"{_title.Trim()}\" by {_artist.Trim()} in your list.";

    private void OnAddFieldChanged() => _confirmedDuplicate = false;

    private void SortBy(SortColumn column)
    {
        if (_sort == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sort = column;
            // Sensible default direction: dates/rating high-first, text A-Z.
            _sortDescending = column is SortColumn.Added or SortColumn.HowItWent or SortColumn.Enjoyment or SortColumn.LastPerformed;
        }
    }

    // The <select> only fires onchange on a real field change, so SortBy always hits its new-field branch (applying
    // that field's sensible default direction); the ▲/▼ button flips direction via SortBy's same-field branch.
    private void OnSortFieldChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<SortColumn>(e.Value?.ToString(), out var column))
            SortBy(column);
    }

    // Year filter: only offered when there are 2+ distinct release years to span. The effective handle
    // positions clamp to [min, max]; a null handle sits at the corresponding bound (i.e. not narrowed).
    private bool ShowYearFilter => _yearDistinctCount >= 2;
    private int YearLo => Math.Clamp(_filterYearLo ?? _yearMin, _yearMin, _yearMax);
    private int YearHi => Math.Clamp(_filterYearHi ?? _yearMax, _yearMin, _yearMax);
    private bool YearFilterActive => ShowYearFilter && (YearLo > _yearMin || YearHi < _yearMax);

    private void OnYearLoInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
            _filterYearLo = Math.Min(Math.Clamp(v, _yearMin, _yearMax), YearHi);   // never cross the high handle
    }

    private void OnYearHiInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
            _filterYearHi = Math.Max(Math.Clamp(v, _yearMin, _yearMax), YearLo);   // never cross the low handle
    }

    // The library's own years within [min, max] — each dropdown passes the other bound, so neither ever lists a
    // year it couldn't take. Widening still works from either end: lowering the earliest re-opens the years
    // below it for the latest, and vice versa.
    //
    // The slider is the exception that forces the merge: it steps through every year in the span and can land on
    // one no song uses, and a select handed a value with no matching <option> silently displays its first option
    // instead of the value.
    private IReadOnlyList<int> YearOptions(int selected, int min, int max)
    {
        var years = _libraryYears.Where(y => y >= min && y <= max).ToList();
        if (!years.Contains(selected))
        {
            years.Add(selected);
            years.Sort();
        }
        return years;
    }

    // Handle position as a % of the track, for the selected-range fill. Invariant culture so CSS never gets a comma.
    private string Pct(int value) =>
        (_yearMax > _yearMin ? (value - _yearMin) * 100.0 / (_yearMax - _yearMin) : 0)
            .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private string PctFromRight(int value) =>
        (_yearMax > _yearMin ? (_yearMax - value) * 100.0 / (_yearMax - _yearMin) : 0)
            .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // Recompute the year bounds from the current list; keep any active selection inside the new range.
    private void RecomputeYearBounds()
    {
        var years = _items.Where(i => i.Year.HasValue).Select(i => i.Year!.Value).ToList();
        _libraryYears = years.Distinct().Order().ToList();
        _yearDistinctCount = _libraryYears.Count;
        if (years.Count > 0)
        {
            _yearMin = years.Min();
            _yearMax = years.Max();
            if (_filterYearLo.HasValue) _filterYearLo = Math.Clamp(_filterYearLo.Value, _yearMin, _yearMax);
            if (_filterYearHi.HasValue) _filterYearHi = Math.Clamp(_filterYearHi.Value, _yearMin, _yearMax);
        }
    }

    // A cheap fingerprint of the active filter + sort; when it changes, the render rewinds infinite scroll to page 1.
    private string FilterSortSignature() => string.Join("",
        _filterSearch,
        string.Join(",", _filterGenres.OrderBy(g => g, StringComparer.Ordinal)),
        string.Join(",", _filterRatings.OrderBy(r => r)),
        string.Join(",", _filterEnjoyments.OrderBy(e => e)),
        string.Join(",", _filterTags.OrderBy(t => t, StringComparer.Ordinal)),
        _tagFilterAll, _filterYearLo, _filterYearHi, _sort, _sortDescending);

    // Grow the window by a page when the sentinel scrolls into range (invoked from khInfinite's IntersectionObserver).
    [JSInvokable]
    public void LoadMore()
    {
        if (_renderCount >= _visibleCount)
            return;
        _renderCount = Math.Min(_renderCount + PageSize, _visibleCount);
        StateHasChanged();
    }

    private bool AnyFilterActive =>
        !string.IsNullOrWhiteSpace(_filterSearch) ||
        AnyHiddenFilterActive;

    // The filters behind the funnel — the search bar shows its own text, so the funnel's dot (and the pills
    // above the list) reflect only these.
    private bool AnyHiddenFilterActive =>
        _filterGenres.Count > 0 ||
        _filterRatings.Count > 0 ||
        _filterEnjoyments.Count > 0 ||
        _filterTags.Count > 0 ||
        YearFilterActive;

    // ---- View-state persistence (filter + sort survive a tab change; held on IAppSession) ----

    // Skipped on the first-ever visit (nothing saved yet), which leaves the page's own defaults in place —
    // SaveViewState then seeds the holder on dispose, so the holder never has to know the default sort column.
    private void RestoreViewState()
    {
        var s = Session.MySongsViewFor(_viewSinger);
        if (!s.Initialized)
            return;

        _filterSearch = s.FilterSearch;
        _filterGenres.Clear();
        foreach (var g in s.FilterGenres) _filterGenres.Add(g);
        _filterRatings.Clear();
        foreach (var r in s.FilterRatings) _filterRatings.Add(r);
        _filterEnjoyments.Clear();
        foreach (var en in s.FilterEnjoyments) _filterEnjoyments.Add(en);
        _filterTags.Clear();
        foreach (var t in s.FilterTags) _filterTags.Add(t);
        _tagFilterAll = s.FilterTagsAll;
        _filterYearLo = s.FilterYearLo;
        _filterYearHi = s.FilterYearHi;
        _sort = (SortColumn)s.Sort;
        _sortDescending = s.SortDescending;
        // Restore the paged count so the returning list rebuilds to the same height (scroll.js can then land the
        // saved scroll). Seed _lastListSig to the restored filter/sort so the first render doesn't rewind to page 1.
        _renderCount = Math.Max(PageSize, s.RenderCount);
        _lastListSig = FilterSortSignature();
    }

    // Reset the page's filter/sort/paging fields to their first-visit defaults — used when switching to a singer
    // whose view state hasn't been seeded yet, so their list starts clean instead of inheriting the last singer's.
    private void ResetViewBuffers()
    {
        // The incoming singer's list replaces this one, so drop every cover and free its blob URL (the ids won't
        // recur, so keeping them would leak). The new singer's cards re-request theirs as they render.
        _ = AlbumArt.ClearAsync();

        _filterSearch = string.Empty;
        _filterGenres.Clear();
        _filterRatings.Clear();
        _filterEnjoyments.Clear();
        _filterTags.Clear();
        _tagFilterAll = false;
        _filterYearLo = null;
        _filterYearHi = null;
        _sort = SortColumn.Title;
        _sortDescending = false;
        _renderCount = PageSize;
        _lastListSig = FilterSortSignature();   // seed so the first render after a switch doesn't rewind paging
    }

    private void SaveViewState(Guid? singer)
    {
        var s = Session.MySongsViewFor(singer);
        s.FilterSearch = _filterSearch;
        s.FilterGenres = new HashSet<string>(_filterGenres, StringComparer.OrdinalIgnoreCase);
        s.FilterRatings = [.. _filterRatings];
        s.FilterEnjoyments = [.. _filterEnjoyments];
        s.FilterTags = new HashSet<string>(_filterTags, StringComparer.OrdinalIgnoreCase);
        s.FilterTagsAll = _tagFilterAll;
        s.FilterYearLo = _filterYearLo;
        s.FilterYearHi = _filterYearHi;
        s.Sort = (int)_sort;
        s.SortDescending = _sortDescending;
        s.RenderCount = _renderCount;
        s.Initialized = true;
    }

    private void ClearFilters()
    {
        _filterSearch = string.Empty;
        ClearAdvancedFilters();
    }

    // Just the funnel filters (genre / how it went / enjoyment / tags / year); the search text is left alone.
    private void ClearAdvancedFilters()
    {
        _filterGenres.Clear();
        _filterRatings.Clear();
        _filterEnjoyments.Clear();
        _filterTags.Clear();
        _filterYearLo = _filterYearHi = null;
    }

    // ---- The funnel's active filters as removable pills ----
    private sealed record FilterPill(string Label, Action Remove);

    // Removing a pill only mutates local filter state — the @onclick re-renders — so there's no store write.
    private List<FilterPill> FilterPills()
    {
        var pills = new List<FilterPill>();
        foreach (var g in _filterGenres.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
            pills.Add(new FilterPill(g, () => _filterGenres.Remove(g)));
        foreach (var r in _filterRatings.OrderByDescending(r => r))
            pills.Add(new FilterPill(r == 0 ? "How it went · unrated" : $"How it went {RatingOptionLabel(r)}",
                () => _filterRatings.Remove(r)));
        foreach (var en in _filterEnjoyments.OrderByDescending(e => e))
            pills.Add(new FilterPill(en == 0 ? "Enjoyment · unrated" : $"Enjoyment {RatingOptionLabel(en)}",
                () => _filterEnjoyments.Remove(en)));
        foreach (var t in _filterTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
            pills.Add(new FilterPill(t, () => _filterTags.Remove(t)));
        if (YearFilterActive)
            pills.Add(new FilterPill($"{YearLo}–{YearHi}", () => { _filterYearLo = _filterYearHi = null; }));
        return pills;
    }

    private static string RatingOptionLabel(int val) => RatingOptions.First(o => o.val == val).label;

    private void OpenFilterSheet() => _filterSheetOpen = true;

    private void CloseFilterSheet() => _filterSheetOpen = false;

    // Pull-down-to-dismiss from khSheet (the filter sheet already animated off-screen).
    [JSInvokable]
    public void CloseFilterSheetFromSwipe()
    {
        CloseFilterSheet();
        StateHasChanged();
    }

    // ---- Add-a-song sheet (floating "+") ----
    private void OpenAdd() => _addOpen = true;

    private void CloseAdd() => _addOpen = false;

    // Pull-down-to-dismiss from khSheet (the add sheet already animated off-screen).
    [JSInvokable]
    public void CloseAddFromSwipe()
    {
        CloseAdd();
        StateHasChanged();
    }

    // The floating "+" tucks away whenever any overlay is up (so it never sits under a dim backdrop) and during the
    // undo-toast window (so a round button doesn't poke out behind the toast). Mirrors TryCloseTopOverlay's set.
    private bool AnyOverlayOpen =>
        _addOpen || _filterSheetOpen || _detailItem is not null || _historyItem is not null
        || _lyricsItem is not null || _ratingPromptItem is not null || _undoItem is not null
        || _surpriseSheetOpen || _rollPick is not null || _historySheetRef?.HasOverlayOpen == true;

    // ---- Multi-select filter helpers ----
    private void SetGenreFilter(string genre, bool on) { if (on) _filterGenres.Add(genre); else _filterGenres.Remove(genre); }
    private void SetRatingFilter(int value, bool on) { if (on) _filterRatings.Add(value); else _filterRatings.Remove(value); }
    private void SetEnjoymentFilter(int value, bool on) { if (on) _filterEnjoyments.Add(value); else _filterEnjoyments.Remove(value); }
    private void SetTagFilter(string tag, bool on) { if (on) _filterTags.Add(tag); else _filterTags.Remove(tag); }

    // Every tag in use, doubling as the Tags filter's options and the tag inputs' suggestions — no separate tag
    // catalogue is kept. Memoized because it's a parameter of the always-rendered detail sheet: as a plain
    // property it walked every song's tags on every repaint. Invalidated when _items reloads (RefreshAsync).
    private IReadOnlyList<string>? _usedTagsCache;
    private IReadOnlyList<string> UsedTags => _usedTagsCache ??= _items
        .SelectMany(i => i.Tags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private string TagSummary => _filterTags.Count == 0
        ? "Any tag"
        : SummaryList(UsedTags.Where(_filterTags.Contains).ToList());

    // Only genres that at least one song actually uses — no point offering a filter that matches nothing.
    private IReadOnlyList<string> UsedGenres => _items
        .Select(i => i.Genre)
        .Where(g => !string.IsNullOrWhiteSpace(g))
        .Select(g => g!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private string GenreSummary => _filterGenres.Count == 0
        ? "Any genre"
        : SummaryList(Genres.All.Where(_filterGenres.Contains).ToList());
    private string RatingSummary(HashSet<int> set) => set.Count == 0
        ? "Any"
        : SummaryList(RatingOptions.Where(o => set.Contains(o.val)).Select(o => o.label).ToList());
    private static string SummaryList(IReadOnlyList<string> labels) =>
        labels.Count <= 2 ? string.Join(", ", labels) : $"{labels[0]}, {labels[1]} +{labels.Count - 2}";

    private IEnumerable<SongListItem> Filtered()
    {
        IEnumerable<SongListItem> q = _items;

        if (!string.IsNullOrWhiteSpace(_filterSearch))
        {
            var term = _filterSearch.Trim();
            q = q.Where(i => i.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                          || i.Artist.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (_filterGenres.Count > 0)
            q = q.Where(i => i.Genre is not null && _filterGenres.Contains(i.Genre));
        if (_filterRatings.Count > 0)
            q = q.Where(i => _filterRatings.Contains(RoundedAvg(i)));
        if (_filterEnjoyments.Count > 0)
            q = q.Where(i => _filterEnjoyments.Contains(i.Enjoyment));
        if (_filterTags.Count > 0)
            q = _tagFilterAll
                ? q.Where(i => _filterTags.All(f => i.Tags.Contains(f, StringComparer.OrdinalIgnoreCase)))
                : q.Where(i => i.Tags.Any(t => _filterTags.Contains(t)));
        if (YearFilterActive)
            q = q.Where(i => i.Year.HasValue && i.Year.Value >= YearLo && i.Year.Value <= YearHi);

        return q;
    }

    private IEnumerable<SongListItem> Sorted()
    {
        var filtered = Filtered();
        IEnumerable<SongListItem> ascending = _sort switch
        {
            SortColumn.Title => filtered.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase),
            SortColumn.Artist => filtered.OrderBy(i => i.Artist, StringComparer.OrdinalIgnoreCase),
            SortColumn.HowItWent => filtered.OrderBy(AvgSortKey),
            SortColumn.Enjoyment => filtered.OrderBy(i => i.Enjoyment),
            SortColumn.LastPerformed => filtered.OrderBy(LastPerformedSortKey),
            _ => filtered.OrderBy(i => i.AddedAt),
        };
        var ordered = _sortDescending ? ascending.Reverse() : ascending;
        // Optionally float favorites above everything, whatever the active sort/direction is.
        // OrderBy is a stable sort, so the chosen order is preserved within each group.
        return Settings.FloatFavoritesToTop ? ordered.OrderBy(i => i.IsFavorite ? 0 : 1) : ordered;
    }

    protected override async Task OnInitializedAsync()
    {
        _viewSinger = Session.ActiveSingerId;   // whose view state we're loading (keys RestoreViewState + scroll)
        RestoreViewState();   // before the first render, so restored filters/sort show immediately
        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseTopOverlay,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnStoreChanged;
        Tonight.Changed += OnTonightChanged;
        // Covers land one at a time after the render that asked for them, and the card's art is a Blazor-rendered
        // inline style — so without this the page would only pick them up on some unrelated re-render.
        AlbumArt.Changed += OnArtChanged;
        // Track the active venue's KaraFun ID so the detail-sheet "Find on KaraFun" button follows it.
        Session.ActiveVenueChanged += OnVenueContextChanged;
        Venues.Changed += OnVenueContextChanged;
        await RefreshActiveVenueKaraFunAsync();

        // Initial load: show the equalizer loader (not the "No songs yet" empty state) while the on-device
        // list deserializes. Hold it for a ~250ms minimum so a fast load doesn't flash it, then reveal the list.
        var startedAt = Clock.GetTimestamp();
        await RefreshAsync();

        // Landing, once per launch: "smart" opens onto Tonight only when a set is queued, "tonight" always does,
        // "songs" never. Every later navigation is left alone, so tapping "My Songs" never bounces away.
        if (!Session.LandingResolved)
        {
            Session.LandingResolved = true;
            var landOnTonight = Settings.LaunchDestination switch
            {
                "songs" => false,
                "tonight" => true,
                _ => _tonight.Count > 0,
            };
            if (Settings.TonightEnabled && landOnTonight)
            {
                Nav.NavigateTo("tonight", replace: true);
                return;   // we're leaving this page; skip the loader-reveal below
            }
        }

        var elapsed = Clock.GetElapsedTime(startedAt);
        if (elapsed < TimeSpan.FromMilliseconds(250))
            await Task.Delay(TimeSpan.FromMilliseconds(250) - elapsed);
        _loading = false;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Gated on the surface signature: re-wiring scans the whole DOM, and most repaints change no art elements.
        if (_artSurfaceSig != _artObservedSig)
        {
            _artObservedSig = _artSurfaceSig;
            await AlbumArt.ObserveAsync();
        }

        // Attach the listener only AFTER restoring: a tab change fires a mount-time scroll-to-0, and a listener
        // attached earlier would record that 0 and clobber the saved position. Waiting for !_loading also means
        // the list has painted, so the page has its full height to scroll to.
        if (!_scrollRestored && !_loading)
        {
            _scrollRestored = true;
            await JS.InvokeVoidAsync("khScroll.restore", ScrollKey);
            await JS.InvokeVoidAsync("khScroll.track", ScrollKey);
            _pagingReset = false;   // the restore positioned this first paint; don't also jump to top
        }
        else if (_pagingReset)
        {
            // A filter/sort change rewound to page 1 — show the new results from the top (and stop the retained deep
            // scroll from instantly tripping the sentinel to load extra pages).
            _pagingReset = false;
            await JS.InvokeVoidAsync("khInfinite.suspend", 400);
            await JS.InvokeVoidAsync("khScroll.toTop");
        }

        // Add-form year input needs the digit-only guard. The form is collapsible, so re-bind whenever it's
        // (re)shown — collapsing remounts a fresh input that has lost the guard.
        if (_addOpen && !_addFieldsBound)
        {
            await JS.InvokeVoidAsync("khNumeric.register");
            _addFieldsBound = true;
        }
        else if (!_addOpen)
        {
            _addFieldsBound = false;
        }

        if (_listRendered && !_swipeBound)
        {
            _swipeRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSwipe.register", _swipeRoot, _swipeRef);
            _swipeBound = true;
        }
        else if (!_listRendered)
        {
            // Card list unmounted (list emptied) — the next mount is a fresh container that needs rebinding.
            _swipeBound = false;
        }

        // The sentinel unmounts once everything's rendered, so disconnect then and re-observe when a filter
        // change brings it back.
        var wantSentinel = _listRendered && _renderCount < _visibleCount;
        if (wantSentinel && !_infiniteBound)
        {
            _swipeRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khInfinite.observe", _sentinel, _swipeRef);
            _infiniteBound = true;
        }
        else if (!wantSentinel && _infiniteBound)
        {
            await JS.InvokeVoidAsync("khInfinite.disconnect");
            _infiniteBound = false;
        }



        // Lyrics sheet: pull down to dismiss; it scrolls, so the same scroll-aware, stronger swipe as history.
        if (_lyricsItem is not null && !_lyricsSheetBound)
        {
            _swipeRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSheet.register", _lyricsSheet, _swipeRef,
                new { closeMethod = "CloseLyricsFromSwipe", closePx = 150 });
            _lyricsSheetBound = true;
        }
        else if (_lyricsItem is null)
        {
            _lyricsSheetBound = false;
        }

        // Filter sheet: pull down to dismiss; it can scroll (year slider + long genre lists), so the same
        // scroll-aware, stronger swipe as the other tall sheets.
        if (_filterSheetOpen && !_filterSheetBound)
        {
            _swipeRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSheet.register", _filterSheet, _swipeRef,
                new { closeMethod = "CloseFilterSheetFromSwipe", closePx = 150 });
            _filterSheetBound = true;
        }
        else if (!_filterSheetOpen)
        {
            _filterSheetBound = false;
        }

        // Add sheet: pull down to dismiss; it can scroll (tags/year), so the same stronger, scroll-aware swipe.
        if (_addOpen && !_addSheetBound)
        {
            _swipeRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khSheet.register", _addSheet, _swipeRef,
                new { closeMethod = "CloseAddFromSwipe", closePx = 150 });
            _addSheetBound = true;
        }
        else if (!_addOpen)
        {
            _addSheetBound = false;
        }

        // Reconcile the page-scroll lock straight from sheet state every render, so it can never get stranded
        // on a close path that skips cleanup (e.g. a touch swipe-dismiss). Only touch JS when it actually flips.
        var anySheetOpen = _detailItem is not null || _historyItem is not null || _ratingPromptItem is not null
            || _lyricsItem is not null || _filterSheetOpen || _addOpen;
        if (anySheetOpen != _pageLocked)
        {
            _pageLocked = anySheetOpen;
            await JS.InvokeVoidAsync("khSheet.setLock", anySheetOpen);
        }

        // Bind the anti-cross clamp whenever the year slider is on screen. It unmounts when the
        // filter is collapsed or drops below 2 distinct years, so re-bind the fresh inputs on return.
        var sliderPresent = _items.Count > 0 && _filterSheetOpen && ShowYearFilter;
        if (sliderPresent && !_rangeBound)
        {
            await JS.InvokeVoidAsync("khRange.register");
            _rangeBound = true;
        }
        else if (!sliderPresent)
        {
            _rangeBound = false;
        }

        // A favorite toggle just re-sorted the list; scroll the affected card into its new position.
        if (_scrollToSongId is { } scrollId)
        {
            _scrollToSongId = null;
            await JS.InvokeVoidAsync("khScroll.toSong", scrollId);
        }
    }

    [JSInvokable]
    public async Task RemoveByIdAsync(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return;

        var removed = _items.FirstOrDefault(i => i.Id == guid);
        if (Settings.ConfirmSongDelete)
        {
            _confirmRemove = removed;
            StateHasChanged();   // [JSInvokable] — off the render path, so ask for one
            return;
        }

        await Store.RemoveAsync(guid);
        if (removed is not null)
            ShowUndo(removed);
    }

    private void CancelRemove()
    {
        _confirmRemove = null;
        StateHasChanged();
    }

    private async Task ConfirmRemoveAsync()
    {
        if (_confirmRemove is not { } item)
            return;

        _confirmRemove = null;
        await Store.RemoveAsync(item.Id);
        ShowUndo(item);
    }

    private void ShowUndo(SongListItem item)
    {
        _undoCts?.Cancel();
        _undoCts = new CancellationTokenSource();
        _undoItem = item;
        _ = DismissUndoAfterDelayAsync(_undoCts.Token);
        StateHasChanged();
    }

    private async Task DismissUndoAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(UndoWindowMs, token); }
        catch (TaskCanceledException) { return; }   // superseded by a newer removal or an Undo

        _undoItem = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task UndoRemoveAsync()
    {
        _undoCts?.Cancel();
        var item = _undoItem;
        _undoItem = null;
        if (item is not null)
            await Store.RestoreAsync(item);   // Store.Changed → refresh puts the row back
    }

    // ---- 🎲 Surprise me ----
    // The narrowing and weighting live in SurprisePicker so they can be tested without a UI.
    private Guid? _lastSurpriseId;
    private SongListItem? _rollPick;        // the current suggestion
    private bool _surpriseSheetOpen;

    private SurpriseOptions SurpriseOptions => new(
        SkipSungToday: Settings.SurpriseSkipSungToday,
        FavourWellSung: Settings.SurpriseFavourWellSung,
        NeverSungOnly: Settings.SurpriseNeverSungOnly,
        FavoritesOnly: Settings.SurpriseFavoritesOnly);

    private void OpenSurpriseOptions()
    {
        _rollPick = null;   // the sheet replaces the snackbar; leaving both up stacks them
        _surpriseSheetOpen = true;
    }

    private void CloseSurpriseOptions() => _surpriseSheetOpen = false;

    private Task RollFromSheetAsync()
    {
        _surpriseSheetOpen = false;
        return RollSurpriseAsync();
    }

    private Task RollSurpriseAsync()
    {
        // "Draw only from filtered songs" is the difference between the visible list and the whole library.
        IReadOnlyList<SongListItem> pool = Settings.SurpriseRespectFilters ? Filtered().ToList() : _items;
        var candidates = Surprise.Narrow(pool, SurpriseOptions, Clock.GetLocalNow().Date);

        var pick = Roll(candidates);
        // One reroll so a tap on "Reroll" never lands on the same song twice in a row.
        if (pick is not null && pick.Id == _lastSurpriseId && candidates.Count >= 2)
            pick = Roll(candidates);

        if (pick is null)
            return Task.CompletedTask;

        _lastSurpriseId = pick.Id;
        _rollPick = pick;
        return Task.CompletedTask;
    }

    private SongListItem? Roll(IReadOnlyList<SongListItem> candidates) =>
        Surprise.Pick(candidates, SurpriseOptions, Rng.NextDouble(),
            BayesStar, neutralStar: _ratingContext.PriorMean ?? 3.0);

    private Task OpenRolledAsync()
    {
        if (_rollPick is not { } pick)
            return Task.CompletedTask;
        _rollPick = null;
        return OpenDetailAsync(pick.Id.ToString());
    }

    // The sheet stays open showing it as added, so a reroll can queue several in a row without reopening.
    private async Task AddRolledToTonightAsync()
    {
        if (_rollPick is not { } pick || IsInTonight(pick))
            return;
        await Tonight.AddAsync(pick.Id);
    }

    // A tap on a row (routed from swipe.js) opens that song's detail sheet.
    [JSInvokable]
    public Task OpenDetailAsync(string id)
    {
        if (Guid.TryParse(id, out var guid))
        {
            _detailItem = _items.FirstOrDefault(i => i.Id == guid);
            _ = LoadDetailSuggestionAsync(_detailItem);
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    // Artist is required: the parser rejects an artist-less result, so a call without one can only come back empty.
    // Deliberately NOT gated on genre/year still being blank — the lookup also decides whether the title looks
    // misspelled, and a song can have complete metadata and a wrong title.
    private bool ShouldLookUp(SongListItem item) =>
        Settings.AutoFillMetadata &&
        !item.MetadataLookedUp &&
        !string.IsNullOrWhiteSpace(item.Title) &&
        !string.IsNullOrWhiteSpace(item.Artist);

    // Whether a lookup could still fill something the user can see. Drives the detail sheet's spinner only —
    // a lookup that can only produce a spelling suggestion shouldn't make the genre/year rows read as pending.
    private static bool WouldFillFields(SongListItem item) =>
        string.IsNullOrWhiteSpace(item.Genre) || !item.Year.HasValue;

    // Runs at most once per song (gated on MetadataLookedUp) and fills blanks straight from the match. A transient
    // failure returns null WITHOUT stamping the flag, so a later add/open can retry.
    private async Task<string?> TryAutoFillMetadataAsync(SongListItem item, CancellationToken token)
    {
        if (!ShouldLookUp(item))
            return null;

        Log.LogDebug("Auto-fill lookup start: “{Title}” — “{Artist}”", item.Title, item.Artist);
        TrackLookupResult lookup;
        try
        {
            lookup = await Metadata.LookupAsync(item.Title, item.Artist, token);
        }
        catch (MetadataLookupException ex)
        {
            // network/rate-limit failure — leave the flag unset to retry later
            Log.LogWarning(ex, "Auto-fill lookup failed for “{Title}” — “{Artist}”; will retry later", item.Title, item.Artist);
            return null;
        }

        if (token.IsCancellationRequested)
            return null;

        var meta = lookup.Match;
        Log.LogDebug("Auto-fill lookup done: “{Title}” — “{Artist}” → matched {Matched}, year={Year}, genre={Genre}, cover={HasCover}",
            item.Title, item.Artist,
            meta is null ? "(no match)" : $"“{meta.MatchedTitle} — {meta.MatchedArtist}”",
            meta?.Year, meta?.Genre, meta?.ArtworkUrl is not null);

        var filled = new List<string>();
        if (string.IsNullOrWhiteSpace(item.Genre) && Genres.Map(meta?.Genre) is string g)
        {
            item.Genre = g;
            filled.Add("Genre");
        }
        if (!item.Year.HasValue && meta?.Year is int y)
        {
            item.Year = y;
            filled.Add("Year");
        }

        // The iTunes match carries the cover for free — capture it so enabling album art later is instant. When
        // iTunes has none and album art is on, fall back to Deezer (art only — never its unreliable year/genre).
        var artUrl = meta?.ArtworkUrl;
        var artLookedUp = true;
        if (artUrl is null && Settings.AlbumArtEnabled)
        {
            try
            {
                artUrl = await ArtFallback.FindCoverArtUrlAsync(item.Title, item.Artist, token);
                Log.LogDebug("iTunes had no cover for “{Title}” — “{Artist}”; Deezer fallback → {Result}",
                    item.Title, item.Artist, artUrl is null ? "no cover found" : "cover found");
            }
            catch (CoverArtLookupException ex)
            {
                artLookedUp = false;   // transient Deezer failure — leave art unflagged so it retries later
                Log.LogWarning(ex, "Deezer cover fallback failed for “{Title}” — “{Artist}”; will retry later", item.Title, item.Artist);
            }
        }
        // iTunes offers its near-miss from the call already made; Deezer costs an extra one, so it's asked only
        // when iTunes had nothing AND the exact-title cover search came up empty — a cover found by exact
        // title+artist proves the spelling is already right.
        // Level 0 drops the suggestion without skipping the lookup: auto-fill still wants the year/genre/art.
        var suggestion = Settings.SpellingSuggestionLevel == 0 ? null : lookup.Suggestion;
        if (Settings.SpellingSuggestionLevel > 0 && suggestion is null && meta is null && artUrl is null)
        {
            suggestion = await Spelling.SuggestAsync(item.Title, item.Artist, token);
            if (token.IsCancellationRequested)
                return null;
        }

        item.SuggestedTitle = suggestion?.Title;
        item.SuggestedArtist = suggestion?.Artist;
        if (suggestion is { } s)
            Log.LogInformation("No match for “{Title}” — “{Artist}”; {Source} suggests “{SuggestedTitle}” — “{SuggestedArtist}”",
                item.Title, item.Artist, lookup.Suggestion is null ? "Deezer" : "iTunes", s.Title, s.Artist);

        if (item.ArtworkUrl is null && artUrl is not null)
            item.ArtworkUrl = artUrl;
        item.ArtworkLookedUp = artLookedUp;   // hit or miss across both sources (unless Deezer failed transiently)
        item.MetadataLookedUp = true;   // metadata attempt is done regardless of the art fallback
        await Store.UpdateAsync(item);   // persist flag + any fills; Store.Changed → RefreshAsync

        return filled.Count > 0
            ? $"✨ Auto-filled {JoinFields(filled)} from “{meta!.MatchedTitle} — {meta.MatchedArtist}”."
            : null;
    }

    // Detail-sheet entry point: the fill/persist lives in TryAutoFillMetadataAsync, this only drives the sheet's
    // spinner + note. The cover is the art service's job, driven by the sheet rendering.
    private async Task LoadDetailSuggestionAsync(SongListItem? item)
    {
        _suggestCts?.Cancel();
        _autoFilledNote = null;
        _suggestionLoading = false;

        if (item is null)
            return;

        if (!ShouldLookUp(item))
            return;

        _suggestCts = new CancellationTokenSource();
        var token = _suggestCts.Token;
        _suggestionLoading = WouldFillFields(item);   // the spinner is for the visible year/genre fill only
        try
        {
            var note = await TryAutoFillMetadataAsync(item, token);
            if (token.IsCancellationRequested || _detailItem?.Id != item.Id)
                return;
            _autoFilledNote = note;
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _suggestionLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void CloseDetail()
    {
        _suggestCts?.Cancel();
        _autoFilledNote = null;
        _suggestionLoading = false;
        _detailItem = null;
        // The page-scroll lock is reconciled from state in OnAfterRenderAsync — no explicit unlock needed here.
    }

    // Routes the Android back button to close the top-most sheet instead of navigating; active only while a sheet is open.
    private BackButtonOverlayGuard? _backGuard;

    // Closes the single top-most open overlay, matching the CSS stacking order (confirm pop-up > the --history
    // sub-sheets > the base detail sheet). Returns false when nothing is open (let navigation proceed).
    private bool TryCloseTopOverlay()
    {
        if (_confirmRemove is not null) { CancelRemove(); return true; }
        if (_historySheetRef?.TryCloseTopOverlay() == true) return true;
        if (_ratingPromptItem is not null) { CloseRatingPrompt(); return true; }
        if (_historyItem is not null) { CloseHistory(); return true; }
        if (_lyricsItem is not null) { CloseLyrics(); return true; }
        if (_addOpen) { CloseAdd(); return true; }
        if (_filterSheetOpen) { CloseFilterSheet(); return true; }
        if (_surpriseSheetOpen) { CloseSurpriseOptions(); return true; }
        if (_rollPick is not null) { _rollPick = null; return true; }   // back dismisses the suggestion
        if (_detailItem is not null) { CloseDetail(); return true; }
        return false;
    }

    // Human list: "Genre", "Genre & Year", or (3+) "A, B, & C" — Oxford comma, ampersand before the last.
    private static string JoinFields(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => string.Empty,
        1 => parts[0],
        2 => $"{parts[0]} & {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, & {parts[^1]}",
    };

    private async Task SaveDetailEditAsync(SongDetailSheet.Edit edit)
    {
        if (_detailItem is null)
            return;

        var newTitle = edit.Title;
        var newArtist = edit.Artist;
        // A title/artist change makes the current cover belong to a different song — clear it so a fresh one is
        // fetched rather than keeping the now-wrong image.
        var identityChanged = !string.Equals(newTitle, _detailItem.Title, StringComparison.Ordinal)
                           || !string.Equals(newArtist, _detailItem.Artist, StringComparison.Ordinal);

        _detailItem.Title = newTitle;
        _detailItem.Artist = newArtist;
        _detailItem.Genre = string.IsNullOrWhiteSpace(edit.Genre) ? null : edit.Genre.Trim();
        _detailItem.Notes = string.IsNullOrWhiteSpace(edit.Notes) ? null : edit.Notes.Trim();
        _detailItem.Year = edit.Year;
        _detailItem.Tags = SongTags.Normalize(edit.Tags);

        if (identityChanged)
        {
            _detailItem.ArtworkUrl = null;
            _detailItem.ArtworkLookedUp = false;   // re-open the one-shot art lookup for the new title/artist
            _ = AlbumArt.DropAsync(_detailItem.Id);   // drop the stale cover so the card doesn't keep showing it
            ReopenLookup(_detailItem);
        }

        await Store.UpdateAsync(_detailItem);

        if (identityChanged)
            await LoadDetailSuggestionAsync(_detailItem);   // fetch + cache a fresh cover for the new song
    }

    // ---- ⚠ Spelling suggestion ---------------------------------------------------------------
    // Both answers are terminal: applying re-runs the lookup on the corrected text, and waving it off clears it
    // for good — MetadataLookedUp is already set, so no later lookup can raise it again.

    // A different title/artist invalidates the one-shot lookup and anything it concluded.
    private static void ReopenLookup(SongListItem item)
    {
        item.MetadataLookedUp = false;
        item.SuggestedTitle = null;
        item.SuggestedArtist = null;
    }

    private async Task ApplySuggestionAsync()
    {
        if (_detailItem is not { HasSuggestion: true } item)
            return;

        item.Title = item.SuggestedTitle!;
        item.Artist = item.SuggestedArtist!;
        // The old spelling's cover (Deezer may have found one for the typo) belongs to a different song now.
        item.ArtworkUrl = null;
        item.ArtworkLookedUp = false;
        _ = AlbumArt.DropAsync(item.Id);
        ReopenLookup(item);

        await Store.UpdateAsync(item);
        await LoadDetailSuggestionAsync(item);   // the corrected text should match outright — fill from it
    }

    private async Task DismissSuggestionAsync()
    {
        if (_detailItem is not { HasSuggestion: true } item)
            return;

        item.SuggestedTitle = null;
        item.SuggestedArtist = null;
        await Store.UpdateAsync(item);
    }

    // ---- "Sang it" rating prompt -------------------------------------------------------------

    private SongListItem? _ratingPromptItem;

    // The shared prompt resets its own star/note buffers on open — nothing to clear here.
    private void OpenRatingPrompt(SongListItem? item)
    {
        if (item is not null)
            _ratingPromptItem = item;
    }

    // Dismissed without logging (✕ / backdrop).
    private void CloseRatingPrompt() => _ratingPromptItem = null;

    // Fires on Save, Skip AND swipe-away — all three commit (a skip logs it unrated, 0); only ✕ / backdrop cancel.
    private async Task OnRatingCommitAsync((int howItWent, string? note, DateTimeOffset when) result)
    {
        var item = _ratingPromptItem;
        _ratingPromptItem = null;
        if (item is null)
            return;

        item.Performances.Add(new Performance
        {
            Date = result.when,
            HowItWent = Math.Clamp(result.howItWent, 0, 5),
            Note = string.IsNullOrWhiteSpace(result.note) ? null : result.note.Trim(),
            VenueId = Session.ActiveVenueId,   // tag with wherever they are right now (null when not at a venue)
        });
        item.Status = SongListItemStatus.Sang;
        await Store.UpdateAsync(item);
    }

    // ---- Performance history sheet ------------------------------------------------------------------

    private SongListItem? _historyItem;

    private void OpenHistory()
    {
        _historyItem = _detailItem;
    }

    private void CloseHistory()
    {
        _historyItem = null;
    }

    // The sheet reports an index into Performances; applying and persisting stays here, with every other
    // mutation of the song.
    private async Task UpdatePerformanceDateAsync(int index, DateTimeOffset date)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].Date = date;
        await Store.UpdateAsync(_historyItem);
    }

    // The sheet has already offered an undo by the time this runs, so it doesn't close on the last removal —
    // closing would take the undo with it. An empty history just shows its empty state until dismissed.
    private async Task RemovePerformanceAsync(int index)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances.RemoveAt(index);
        if (_historyItem.Performances.Count == 0)
            _historyItem.Status = SongListItemStatus.WantToSing;   // last performance gone — back on the wishlist
        await Store.UpdateAsync(_historyItem);
    }

    private async Task RestorePerformanceAsync(int index, Performance performance)
    {
        if (_historyItem is null)
            return;

        _historyItem.Performances.Insert(Math.Clamp(index, 0, _historyItem.Performances.Count), performance);
        _historyItem.Status = SongListItemStatus.Sang;
        await Store.UpdateAsync(_historyItem);
    }

    private async Task UpdatePerformanceRatingAsync(int index, int rating)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].HowItWent = Math.Clamp(rating, 0, 5);
        await Store.UpdateAsync(_historyItem);
    }

    private async Task UpdatePerformanceNoteAsync(int index, string? note)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await Store.UpdateAsync(_historyItem);
    }

    // Opens in the OS browser / YouTube app — never the in-app WebView.
    private Task OpenYouTubeAsync() =>
        _detailItem is null
            ? Task.CompletedTask
            : Links.OpenAsync(Links2.YouTubeUrlFor(_detailItem.Title, _detailItem.Artist));

    // Opens in the OS browser / Spotify app — never the in-app WebView.
    private Task OpenSpotifyAsync() =>
        _detailItem is null
            ? Task.CompletedTask
            : Links.OpenAsync(Links2.SpotifyUrlFor(_detailItem.Title, _detailItem.Artist));

    // The button only renders when _activeVenueKaraFunId is set, so it's always present here.
    private Task OpenKaraFunAsync() =>
        _detailItem is null || string.IsNullOrWhiteSpace(_activeVenueKaraFunId)
            ? Task.CompletedTask
            : Links.OpenAsync(Links2.KaraFunUrlFor(_activeVenueKaraFunId, _detailItem.Title, _detailItem.Artist));

    // Split-button variant of the KaraFun action: queue for tonight (unless already there), then open KaraFun.
    private async Task AddToTonightAndOpenKaraFunAsync()
    {
        if (_detailItem is null)
            return;
        if (!IsInTonight(_detailItem))
            await Tonight.AddAsync(_detailItem.Id);
        await OpenKaraFunAsync();
    }

    // Tracked reactively so the "Find on KaraFun" button appears/disappears as the venue is switched or its
    // KaraFun ID is edited.
    private string? _activeVenueKaraFunId;
    private void OnVenueContextChanged(object? sender, EventArgs e) => _ = RefreshActiveVenueKaraFunAsync();

    private async Task RefreshActiveVenueKaraFunAsync()
    {
        var venue = Session.ActiveVenueId is { } id ? await Venues.GetAsync(id) : null;
        var kf = string.IsNullOrWhiteSpace(venue?.KaraFunVenueId) ? null : venue!.KaraFunVenueId;
        if (kf == _activeVenueKaraFunId)
            return;
        _activeVenueKaraFunId = kf;
        await InvokeAsync(StateHasChanged);
    }

    // ---- Lyrics sheet (LRCLIB) ---------------------------------------------------------------
    // State is cleared to the loading view first so a re-open never flashes the previous song's lyrics; the fetch
    // is cancellable so a quick close (or reopening on another song) can't land on stale state.
    private async Task OpenLyricsAsync()
    {
        if (_detailItem is null)
            return;

        _lyricsItem = _detailItem;
        _lyricsText = _lyricsError = null;
        _lyricsInstrumental = false;
        _lyricsLoading = true;

        _lyricsCts?.Cancel();
        _lyricsCts?.Dispose();
        _lyricsCts = new CancellationTokenSource();
        var token = _lyricsCts.Token;

        var title = _lyricsItem.Title;
        var artist = _lyricsItem.Artist;

        try
        {
            LyricsResult? result;
            var useCache = Settings.LyricsCacheEnabled;

            // Read-through cache: a hit (including a cached "no match") skips the network; a miss fetches and
            // writes back. When caching is off, always fetch fresh and store nothing.
            if (useCache && await LyricsCache.GetAsync(title, artist) is { } hit)
            {
                result = hit.Result;
            }
            else
            {
                result = await Lyrics.FetchAsync(title, artist, token);
                if (useCache && !token.IsCancellationRequested)
                    await LyricsCache.SetAsync(title, artist, result);
            }

            if (token.IsCancellationRequested)
                return;

            if (result is null)
                _lyricsText = null;             // no match → "No lyrics found" view
            else if (result.Instrumental)
                _lyricsInstrumental = true;     // matched, but the track has no lyrics
            else
                _lyricsText = result.PlainLyrics;
        }
        catch (OperationCanceledException) { return; }   // superseded / closed — leave state to the newer open
        catch (LyricsLookupException ex) { _lyricsError = ex.Message; }
        catch (Exception ex)
        {
            _lyricsError = "Couldn't load lyrics. Try again.";
            Log.LogWarning(ex, "Lyrics load failed");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _lyricsLoading = false;
                StateHasChanged();
            }
        }
    }

    private void CloseLyrics()
    {
        _lyricsCts?.Cancel();
        _lyricsItem = null;
        // Page-scroll lock is reconciled from state in OnAfterRenderAsync; detail sheet stays open behind this.
    }

    // Pull-down-to-dismiss from khSheet (the lyrics sheet already animated off-screen).
    [JSInvokable]
    public void CloseLyricsFromSwipe()
    {
        CloseLyrics();
        StateHasChanged();
    }

    private async Task AddAsync()
    {
        if (!CanAdd)
            return;

        if (IsDuplicate && !_confirmedDuplicate)
        {
            _confirmedDuplicate = true;   // arm: the next click adds it anyway
            return;
        }

        var added = await Store.AddAsync(_title, _artist, _notes, _genre, _year);
        // AddAsync has no tags parameter — persist any in a follow-up write so the store stays simple.
        var tags = SongTags.Normalize(_tags);
        if (tags.Count > 0)
        {
            added.Tags = tags;
            await Store.UpdateAsync(added);
        }
        // Switch to newest-first so the song they just added is right at the top (favorites still float above it).
        _sort = SortColumn.Added;
        _sortDescending = true;

        _title = _artist = _notes = _genre = string.Empty;    // Store.Changed triggers the list refresh.
        _tags = [];
        _year = null;
        _confirmedDuplicate = false;
        _addOpen = false;   // collapse the form on a successful add; the new song is now visible at the top

        // Background so the add returns immediately and never blocks on the network; when the lookup lands,
        // Store.UpdateAsync fires Changed → the list re-renders with it.
        _ = AutoFillAddedAsync(added);
    }

    // Fire-and-forget from AddAsync: swallows its own failures (a lookup must never crash the app) and is
    // cancelled if the page is disposed mid-flight.
    private async Task AutoFillAddedAsync(SongListItem item)
    {
        try
        {
            await TryAutoFillMetadataAsync(item, _bgFillCts.Token);
            // No cover fetch here: the new card rendering asks the service for one, which discovers it if needed.
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // best-effort background enrichment
            Log.LogWarning(ex, "Background auto-fill for a newly added song failed");
        }
    }

    private async Task ToggleFavoriteAsync(SongListItem item)
    {
        item.IsFavorite = !item.IsFavorite;
        // Set before the await so the render triggered by the store change carries the pending scroll.
        if (Settings.ScrollToFavorited)
        {
            _scrollToSongId = item.Id.ToString();
            // Un-favoriting drops the song to its sorted position, which may be past the paged window; grow the
            // window to include it so the card exists in the DOM for khScroll.toSong to reach.
            var idx = Sorted().ToList().FindIndex(i => i.Id == item.Id);
            if (idx >= _renderCount)
                _renderCount = idx + 1;
        }
        await Store.UpdateAsync(item);
    }

    // The confidence-weighted "how it went" star (Bayesian shrinkage toward the list average), rebuilt whenever the
    // list reloads. The card star, the sort and the rating filter buckets all read this — keep them on it.
    private RatingContext _ratingContext = new(null, RatingConfig.Default, default);
    private double? BayesStar(SongListItem item) => Scorer.StarFor(item, _ratingContext);

    // Whole-star value for the red "how it went" display: the rounded Bayesian star (0 when the song has no rating).
    private int RoundedAvg(SongListItem item) =>
        BayesStar(item) is { } score ? (int)Math.Round(score, MidpointRounding.AwayFromZero) : 0;

    // Sort key for "how it went": the Bayesian star; unrated/unsung songs sort below every rated one.
    private double AvgSortKey(SongListItem item) => BayesStar(item) ?? -1;

    // Sort key for "Sang": when the song was last sung. Never-sung songs take the earliest possible date so
    // they land at the bottom in the default (most-recent-first) direction, the way unrated songs do.
    private static DateTimeOffset LastPerformedSortKey(SongListItem item) => item.LastSungAt ?? DateTimeOffset.MinValue;

    private void OnArtChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    // Enjoyment is independent of sung-state — a never-sung song can still carry one.
    private async Task UpdateEnjoymentAsync(SongListItem item, int rating)
    {
        item.Enjoyment = Math.Clamp(rating, 0, 5);
        await Store.UpdateAsync(item);
    }

    // Not `async void`: InvokeAsync marshals the reload onto the render thread (Changed can fire from a background
    // thread), and the task swallows its own failures so nothing is left unobserved.
    private void OnStoreChanged(object? sender, EventArgs e) => _ = RefreshFromStoreAsync();

    private async Task RefreshFromStoreAsync()
    {
        try
        {
            await InvokeAsync(RefreshAsync);
        }
        catch (Exception ex)
        {
            // Best-effort refresh: keep the current list on screen if a reload fails.
            Log.LogWarning(ex, "My Songs reload after a store change failed; keeping last-known list");
        }
    }

    private async Task RefreshAsync()
    {
        // A singer switch keeps this page mounted but fires the store's Changed (its file changed under it), so
        // this is the only place it can be detected: stash the outgoing singer's view state, load the incoming
        // one's, and re-arm the scroll restore so OnAfterRender lands their position once the new list paints.
        var current = Session.ActiveSingerId;
        if (current != _viewSinger)
        {
            SaveViewState(_viewSinger);
            _ = JS.InvokeVoidAsync("khScroll.untrack", ScrollKeyFor(_viewSinger));
            _viewSinger = current;
            ResetViewBuffers();
            RestoreViewState();
            _scrollRestored = false;
        }

        _items = await Store.GetAllAsync();
        _itemsVersion++;         // invalidate the sorted cache and re-wire the art observer
        _usedTagsCache = null;
        // The prior is list-wide, so the context is rebuilt once per load, never per song.
        _ratingContext = Scorer.BuildContext(_items,
            RatingConfig.Default with
            {
                RecencyEnabled = Settings.RecencyWeightedRatings,
                PriorWeight = Settings.RatingPriorWeight,
                HalfLifeDays = Settings.RecencyHalfLifeDays,
            },
            Clock.GetLocalNow());
        RecomputeYearBounds();
        await Tonight.PruneAsync(_items.Select(i => i.Id).ToList());
        _tonight = await Tonight.GetAllAsync();
        _tonightIds = [.. _tonight.Select(e => e.SongId)];
        await InvokeAsync(StateHasChanged);
    }

    // Tonight store changed (add/remove/reorder/complete/clear) — reload just the set and re-render.
    private void OnTonightChanged(object? sender, EventArgs e) => _ = RefreshTonightAsync();

    private async Task RefreshTonightAsync()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                _tonight = await Tonight.GetAllAsync();
                _tonightIds = [.. _tonight.Select(e => e.SongId)];
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            // best-effort: keep the last-known set on screen if a reload fails
            Log.LogWarning(ex, "Tonight refresh on My Songs failed; keeping last-known set");
        }
    }

    // ---- Tonight quick-add (from the wishlist) -----------------------------------------------

    // A set, not a scan of _tonight: this runs several times per card per render.
    private HashSet<Guid> _tonightIds = [];
    private bool IsInTonight(SongListItem? item) => item is not null && _tonightIds.Contains(item.Id);

    // No remove-confirm here: re-adding is one tap. The confirmed removal lives on the Tonight tab's ✕.
    private Task QuickToggleTonightAsync(SongListItem item) =>
        IsInTonight(item) ? Tonight.RemoveAsync(item.Id) : Tonight.AddAsync(item.Id);

    private async Task ToggleDetailTonightAsync()
    {
        if (_detailItem is null)
            return;
        // The detail-sheet toggle is an explicit on/off, so no remove-confirm here.
        if (IsInTonight(_detailItem))
            await Tonight.RemoveAsync(_detailItem.Id);
        else
            await Tonight.AddAsync(_detailItem.Id);
    }

    public void Dispose()
    {
        SaveViewState(_viewSinger);
        _ = JS.InvokeVoidAsync("khScroll.untrack", ScrollKey);
        _ = JS.InvokeVoidAsync("khInfinite.disconnect");
        Store.Changed -= OnStoreChanged;
        Tonight.Changed -= OnTonightChanged;
        Session.ActiveVenueChanged -= OnVenueContextChanged;
        Venues.Changed -= OnVenueContextChanged;
        _backGuard?.Dispose();
        // Safety net: never leave the page-scroll lock on if this page is torn down with a sheet open.
        if (_pageLocked)
            _ = JS.InvokeVoidAsync("khSheet.setLock", false);
        _swipeRef?.Dispose();
        _undoCts?.Cancel();
        _undoCts?.Dispose();
        _suggestCts?.Cancel();
        _suggestCts?.Dispose();
        _lyricsCts?.Cancel();
        _lyricsCts?.Dispose();
        _bgFillCts.Cancel();
        _bgFillCts.Dispose();
        AlbumArt.Changed -= OnArtChanged;
        // Covers deliberately survive this page: the art service outlives it, the same songs are still the
        // singer's, and eviction already bounds what's held. Clearing here made every tab switch re-download
        // the list. A singer switch still clears (ResetViewBuffers) — that's when the ids stop being ours.
    }
}
