namespace KHost.Mobile.Services;

/// <summary>
/// The My Songs list's filter + sort presentation, held on <see cref="IAppSession"/> so it survives leaving and
/// returning to the tab within a launch (the page component itself is disposed on navigation, so its own fields
/// reset). Plain mutable state — the page copies it in on init and writes it back on dispose. Scroll position is
/// kept separately in <c>scroll.js</c> module state (it survives SPA navigation without any interop).
/// </summary>
public sealed class MySongsViewState
{
    /// <summary>
    /// False until the page has written its state here at least once. On the first visit the page keeps its own
    /// defaults (and seeds this holder on dispose) rather than reading the not-yet-populated values; every later
    /// visit restores from here. Avoids this holder having to know the page's private default sort column.
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>The single search box, matched against a song's title OR artist.</summary>
    public string FilterSearch { get; set; } = string.Empty;
    public HashSet<string> FilterGenres { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<int> FilterRatings { get; set; } = [];
    public HashSet<int> FilterEnjoyments { get; set; } = [];
    public HashSet<string> FilterTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tag filter combine mode: false = match ANY selected tag (OR, the default), true = match ALL (AND).</summary>
    public bool FilterTagsAll { get; set; }

    public int? FilterYearLo { get; set; }
    public int? FilterYearHi { get; set; }

    /// <summary>The active sort column, stored as the page's private <c>SortColumn</c> enum value (cast to int).</summary>
    public int Sort { get; set; }
    public bool SortDescending { get; set; }

    /// <summary>How many cards the infinite-scroll list had rendered when the tab was left. Restored so the returning
    /// page rebuilds to the same height and <c>scroll.js</c> can put the saved scroll position back.</summary>
    public int RenderCount { get; set; }
}
