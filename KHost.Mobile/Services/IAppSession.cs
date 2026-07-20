namespace KHost.Mobile.Services;

/// <summary>
/// Ephemeral, per-launch app state that isn't persisted (unlike <see cref="IAppSettings"/>). Lives as a singleton
/// so it's shared app-wide for the life of the process and resets on the next launch.
/// </summary>
public interface IAppSession
{
    /// <summary>
    /// Whether the one-time "smart landing" decision has been made this launch. The first time My Songs loads we
    /// route to the Tonight tab if a set is queued, otherwise stay on My Songs; every later navigation is left
    /// alone (so tapping "My Songs" doesn't bounce back to Tonight). Set once, then never re-evaluated this launch.
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
    /// The My Songs list's filter + sort + scroll state <em>for a given singer</em>, kept here so it survives
    /// leaving and returning to the tab within a launch (the page component is disposed on navigation, so its own
    /// fields would otherwise reset) — and so switching singers restores each person's own filters, sort, paging and
    /// scroll position. One instance per singer id, created on first ask; <paramref name="singerId"/> null returns a
    /// shared fallback (the session-less / pre-seed path). Never persisted.
    /// </summary>
    MySongsViewState MySongsViewFor(Guid? singerId);

    /// <summary>
    /// The venue the singer is "at" right now — where performances get tagged and which catalog the header chip
    /// reflects. Ephemeral like the rest of the session: it is re-resolved each launch (manually via the switcher
    /// today, by geolocation later) and never persisted. Null means "not at a saved venue" (performances then log
    /// untagged). Set via <see cref="SetActiveVenue"/> so <see cref="ActiveVenueChanged"/> can fire.
    /// </summary>
    Guid? ActiveVenueId { get; }

    /// <summary>
    /// Whether the active venue was chosen manually (the switcher / a "Set active") rather than resolved from
    /// location. While pinned, the periodic geo re-check leaves the active venue alone so a deliberate pick isn't
    /// stomped. Cleared by "resume auto-detect" (and by the next launch, since the session resets).
    /// </summary>
    bool ActiveVenuePinned { get; }

    /// <summary>Set (or clear, with null) the <see cref="ActiveVenueId"/>, recording whether it was a manual
    /// (<paramref name="pinned"/>) choice. Raises <see cref="ActiveVenueChanged"/> only when the venue actually
    /// changes, so the header chip and Venues page refresh in lock-step; the pin state always updates.</summary>
    void SetActiveVenue(Guid? venueId, bool pinned = false);

    /// <summary>Raised after <see cref="ActiveVenueId"/> changes (manual switch, geo re-check, or a delete that
    /// clears it). Lets the header chip and any open venue view refresh without polling.</summary>
    event EventHandler? ActiveVenueChanged;

    /// <summary>
    /// The singer whose personal lists (My Songs + Tonight) the app is currently showing. Multiple singers share the
    /// device; this ephemeral pointer selects whose data the per-singer stores read/write. Re-resolved each launch
    /// (from <see cref="IAppSettings.LastActiveSingerId"/> when still valid, else the first singer) and never
    /// persisted here. The bootstrap sets it before any personal page loads, so it is expected non-null in the UI.
    /// Set via <see cref="SetActiveSinger"/> so <see cref="ActiveSingerChanged"/> can fire.
    /// </summary>
    Guid? ActiveSingerId { get; }

    /// <summary>Set the <see cref="ActiveSingerId"/>. Raises <see cref="ActiveSingerChanged"/> only when the singer
    /// actually changes, so the per-singer stores reload and the header avatar + accent refresh in lock-step.</summary>
    void SetActiveSinger(Guid? singerId);

    /// <summary>Raised after <see cref="ActiveSingerId"/> changes (a switch in the header, or a delete that
    /// re-points it). The per-singer stores listen to reload their file; the header chip re-tints the app.</summary>
    event EventHandler? ActiveSingerChanged;

    /// <summary>The venue whose detail sheet the first-run tour wants open (or null to close it). Set only by the
    /// tutorial so its Venues-chapter steps can spotlight controls inside the detail sheet (the KaraFun catalog,
    /// the toggles, the history); the Venues page listens via <see cref="TutorialVenueDetailChanged"/> and reflects
    /// it. Purely a transient tour affordance — never persisted.</summary>
    Guid? TutorialVenueDetailId { get; }

    /// <summary>Set (or clear, with null) <see cref="TutorialVenueDetailId"/> and raise
    /// <see cref="TutorialVenueDetailChanged"/> so the Venues page opens or closes that venue's detail.</summary>
    void SetTutorialVenueDetail(Guid? venueId);

    /// <summary>Raised when the tutorial asks the Venues page to open or close a venue's detail sheet.</summary>
    event EventHandler? TutorialVenueDetailChanged;
}
