namespace KHost.Mobile.Services;

/// <summary>
/// The Tonight tab's persisted view state, held on <see cref="IAppSession"/> so it survives leaving and returning
/// to the tab within a launch (the page component is disposed on navigation, so its own fields reset). Mirrors
/// <see cref="MySongsViewState"/> — currently just the scroll offset, but it's the home for any future Tonight
/// view state (a filter, a sort) so it can be added here without new plumbing. The page pushes the scroll offset
/// in via a debounced JS callback and reads it back on the next visit; see <c>scroll.js</c>.
/// </summary>
public sealed class TonightViewState
{
    /// <summary>Last window scroll offset, restored when the tab is reopened. 0 = top / not yet scrolled.</summary>
    public double ScrollTop { get; set; }
}
