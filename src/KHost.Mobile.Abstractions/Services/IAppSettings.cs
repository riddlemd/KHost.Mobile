namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// User-adjustable app preferences, persisted across launches. Every flag defaults to <c>true</c> unless its
/// summary says otherwise, so a fresh install behaves as it did before the setting existed.
/// </summary>
public interface IAppSettings
{
    /// <summary>When true, iTunes is used to auto-fill a song's blank year/genre. When false, no iTunes lookups run.</summary>
    bool AutoFillMetadata { get; set; }

    /// <summary>
    /// When true, the "Tonight" on-deck set list is available. When false it's hidden everywhere, but the saved set
    /// is left untouched, so turning it back on restores it.
    /// </summary>
    bool TonightEnabled { get; set; }

    /// <summary>
    /// The id (as a string) of the singer that was active when the app last closed. Empty when never set; the
    /// bootstrap falls back to the first singer when it's empty or names a singer that no longer exists.
    /// String-typed to sit in the same key/value store as the rest.
    /// </summary>
    string LastActiveSingerId { get; set; }

    /// <summary>When true, the YouTube quick link is offered for a song.</summary>
    bool YouTubeSearchEnabled { get; set; }

    /// <summary>When true, the Spotify quick link is offered for a song.</summary>
    bool SpotifySearchEnabled { get; set; }

    /// <summary>
    /// When true, every KaraFun surface is offered: the per-song quick link, a venue's catalog button and its QR
    /// code, and the KaraFun venue ID field (with its QR scan) on the venue editor. Off hides all of them; a
    /// venue keeps any ID already saved.
    /// </summary>
    bool KaraFunFeaturesEnabled { get; set; }


    /// <summary>
    /// When true, the active venue is auto-selected from the device's location, re-checking every
    /// <see cref="VenueRecheckMinutes"/> minutes while the app is in the foreground. Stays dormant — and does not
    /// request the location permission — until at least one venue has a saved point to match against. Manually
    /// picking a venue pins it until "resume auto-detect".
    /// </summary>
    bool LocationAutoDetect { get; set; }

    /// <summary>
    /// How often (minutes) location auto-detect re-checks the current venue while the app is open. Defaults to 5;
    /// the tracker clamps to ~2–30.
    /// </summary>
    int VenueRecheckMinutes { get; set; }


    /// <summary>When true, LRCLIB lyrics lookup is offered for a song.</summary>
    bool LyricsEnabled { get; set; }

    /// <summary>
    /// When true, looked-up lyrics are cached on-device so re-opening a song's lyrics skips the network. When
    /// false, every open re-fetches from LRCLIB (and nothing new is written to the cache).
    /// </summary>
    bool LyricsCacheEnabled { get; set; }

    /// <summary>When true, favoriting a song scrolls the list to reveal that song's new position.</summary>
    bool ScrollToFavorited { get; set; }

    /// <summary>
    /// When true, a song's free-form tags are shown and editable, and the Tags filter is offered. When false all of
    /// that is hidden; tags already saved on songs are left untouched, so turning it back on restores them.
    /// </summary>
    bool TagsEnabled { get; set; }

    /// <summary>
    /// When true, each song's cover art is looked up, downloaded and displayed. When false, no cover lookups or
    /// downloads run and no art is shown.
    /// </summary>
    bool AlbumArtEnabled { get; set; }

    /// <summary>When true, the "Surprise me" random picker is available.</summary>
    bool SurpriseEnabled { get; set; }

    /// <summary>
    /// When true, the "Surprise me" random picker skips any song already sung today. If every candidate has already
    /// been sung today it falls back to the full list rather than doing nothing.
    /// </summary>
    bool SurpriseSkipSungToday { get; set; }

    /// <summary>
    /// When true, the "Surprise me" draw is weighted by each song's "how it went" star; unrated songs draw on the
    /// list average and everything keeps a small floor. When false every candidate is equally likely. Defaults to
    /// <c>true</c> — this was the picker's fixed behaviour before the options sheet existed.
    /// </summary>
    bool SurpriseFavourWellSung { get; set; }

    /// <summary>When true, the "Surprise me" draw is limited to songs with no performance history yet.</summary>
    bool SurpriseNeverSungOnly { get; set; }

    /// <summary>When true, the "Surprise me" draw is limited to songs marked as a favorite.</summary>
    bool SurpriseFavoritesOnly { get; set; }

    /// <summary>
    /// When true, the "Surprise me" draw uses only the currently filtered/visible songs rather than the whole list.
    /// Defaults to <c>true</c> — the picker always scoped itself to the filtered list before the options sheet made
    /// the choice explicit.
    /// </summary>
    bool SurpriseRespectFilters { get; set; }

    /// <summary>
    /// When true, marking a song sung asks for a "how it went" star rating. When false the prompt only asks for a note.
    /// </summary>
    bool RatePerformances { get; set; }

    /// <summary>
    /// When true, a song's derived how-it-went star weights recent sings more than old ones (exponential time-decay).
    /// When false (the default), every rated sing counts equally. Affects only the derived star and the ranking that
    /// uses it — the stored performances are untouched.
    /// </summary>
    bool RecencyWeightedRatings { get; set; }

    /// <summary>
    /// When true, the app checks GitHub for a newer release once at startup. When false, no update check runs (no
    /// network request).
    /// </summary>
    bool UpdateCheckEnabled { get; set; }

    /// <summary>
    /// Whether the first-run tutorial has been completed or skipped. Defaults to <c>false</c> (opposite semantics to
    /// the feature flags), so the tour shows once on a fresh install and never again after.
    /// </summary>
    bool TutorialCompleted { get; set; }

    /// <summary>
    /// Comma-joined ids of the user's OWN songs the tutorial queued onto an empty Tonight set, persisted so a tour
    /// interrupted by an app kill can still remove exactly those rows on its next run — the in-memory tracking list
    /// doesn't survive a restart, and those rows aren't the fixed-id samples the self-heal already knows. Empty when
    /// no tour seeding is outstanding.
    /// </summary>
    string TutorialSeededTonightIds { get; set; }

    /// <summary>
    /// When true, gestures that change state (press-and-hold to switch venue/singer, the 🎲 hold) confirm with a
    /// short vibration. When false no haptic fires anywhere.
    /// </summary>
    bool HapticsEnabled { get; set; }

    /// <summary>When true, times render as 24-hour (21:30). When false, as 12-hour with AM/PM.</summary>
    bool Use24HourTime { get; set; }

    /// <summary>
    /// When true, favorited songs are pinned above every other song in My Songs regardless of the chosen sort. When
    /// false, favorites sort with everything else and only the chosen column decides the order.
    /// </summary>
    bool FloatFavoritesToTop { get; set; }

    /// <summary>
    /// When true, removing a song asks for confirmation first. When false, the swipe removes immediately and the
    /// undo snackbar is the only way back.
    /// </summary>
    bool ConfirmSongDelete { get; set; }

    /// <summary>
    /// How long an undo snackbar (removed song, venue, or performance) stays on offer, in seconds.
    /// </summary>
    int UndoWindowSeconds { get; set; }

    /// <summary>
    /// Which page a launch lands on: <c>"songs"</c> and <c>"tonight"</c> always open that tab; <c>"smart"</c> opens
    /// Tonight only when a set is already queued, else My Songs.
    /// </summary>
    string LaunchDestination { get; set; }

    /// <summary>
    /// The half-life, in days, used when <see cref="RecencyWeightedRatings"/> is on — how quickly an older
    /// performance's influence on the derived star decays. Ignored entirely when recency weighting is off.
    /// </summary>
    int RecencyHalfLifeDays { get; set; }

    /// <summary>
    /// How close, in metres, the device must be to a venue's saved location to count as being there. Only consulted
    /// when <see cref="LocationAutoDetect"/> is on. Stored metric whatever <see cref="ShowDistanceInFeet"/> displays.
    /// </summary>
    int VenueDetectionMeters { get; set; }

    /// <summary>
    /// When true (the default), distances are shown in feet. Presentation only — distances are always stored and
    /// compared in metres.
    /// </summary>
    bool ShowDistanceInFeet { get; set; }

    /// <summary>
    /// How many performances a song needs before its derived star trusts its own average rather than leaning on the
    /// list-wide mean. Lower trusts a thin record sooner; higher demands a longer one.
    /// </summary>
    int RatingPriorWeight { get; set; }

    /// <summary>How many go-to songs and recent performances a venue's history lists.</summary>
    int VenueHistoryEntries { get; set; }

    /// <summary>
    /// How readily a near-miss title/artist earns a "Did you mean …?" suggestion: <c>0</c> off, <c>1</c> cautious,
    /// <c>2</c> eager (tolerates more typos, at the cost of offering corrections to deliberate spellings).
    /// </summary>
    int SpellingSuggestionLevel { get; set; }

    /// <summary>
    /// Two-letter store region for catalogue lookups (year, genre, cover art). Picks which country's catalogue and
    /// genre names a lookup returns.
    /// </summary>
    string CatalogueRegion { get; set; }

    /// <summary>
    /// Delay in milliseconds between the per-song lookups an import's review step makes. Lower finishes sooner but
    /// risks the catalogue rate-limiting the batch.
    /// </summary>
    int ImportLookupDelayMs { get; set; }
}
