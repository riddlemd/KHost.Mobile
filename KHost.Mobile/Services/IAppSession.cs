namespace KHost.Mobile.Services;

/// <summary>
/// Ephemeral, per-launch app state that isn't persisted (unlike <see cref="IAppSettings"/>). Lives as a singleton
/// so it's shared app-wide for the life of the process and resets on the next launch.
/// </summary>
public interface IAppSession
{
    /// <summary>
    /// Whether the one-time "smart landing" decision has been made this launch. The first time My Songs loads we
    /// route to the Tonight tab if a set is queued, otherwise stay on My List; every later navigation is left
    /// alone (so tapping "My List" doesn't bounce back to Tonight). Set once, then never re-evaluated this launch.
    /// </summary>
    bool LandingResolved { get; set; }

    /// <summary>
    /// Whether the first-run tutorial's show/skip decision has been made this launch. The durable "has completed
    /// it" flag lives in <see cref="IAppSettings.TutorialCompleted"/>; this transient guard stops the overlay from
    /// re-triggering on every navigation within a single launch (it's set true the moment the tour starts, and the
    /// "Replay tutorial" action clears it to re-arm the check).
    /// </summary>
    bool TutorialResolved { get; set; }

    /// <summary>
    /// The My Songs list's filter + sort state, kept here so it survives leaving and returning to the tab within a
    /// launch (the page component is disposed on navigation, so its own fields would otherwise reset). One shared
    /// instance for the process; the page restores from it on init and writes back on dispose.
    /// </summary>
    MySongsViewState MySongsView { get; }

    /// <summary>
    /// The venue the singer is "at" right now — where performances get tagged and which catalog the header chip
    /// reflects. Ephemeral like the rest of the session: it is re-resolved each launch (manually via the switcher
    /// today, by geolocation later) and never persisted. Null means "not at a saved venue" (performances then log
    /// untagged). Set via <see cref="SetActiveVenue"/> so <see cref="ActiveVenueChanged"/> can fire.
    /// </summary>
    Guid? ActiveVenueId { get; }

    /// <summary>Set (or clear, with null) the <see cref="ActiveVenueId"/>. Raises <see cref="ActiveVenueChanged"/>
    /// only when the value actually changes, so the header chip and Venues page can refresh in lock-step.</summary>
    void SetActiveVenue(Guid? venueId);

    /// <summary>Raised after <see cref="ActiveVenueId"/> changes (manual switch, geo re-check, or a delete that
    /// clears it). Lets the header chip and any open venue view refresh without polling.</summary>
    event EventHandler? ActiveVenueChanged;
}
