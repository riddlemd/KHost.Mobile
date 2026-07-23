namespace KHost.Mobile.Services;

/// <summary>
/// User-adjustable app preferences — simple on/off flags surfaced on the Settings page and persisted across
/// launches. Both default to <c>true</c> so the app behaves exactly as before until the user opts out.
/// </summary>
public interface IAppSettings
{
    /// <summary>
    /// When true, iTunes is used to auto-fill a song's blank year/genre: when a song is added, when the detail
    /// sheet is opened, and via the post-import enrichment/review pass. When false, no iTunes lookups run.
    /// </summary>
    bool AutoFillMetadata { get; set; }

    /// <summary>
    /// When true, the "Tonight" on-deck set list is available: it gets its own tab in the bottom bar and every
    /// song gets a quick "add to tonight" control. When false, the Tonight tab (and with it the whole bottom bar,
    /// since it's then the only extra destination) and the per-song controls are hidden — the saved set is left
    /// untouched, so turning it back on restores it.
    /// </summary>
    bool TonightEnabled { get; set; }

    /// <summary>
    /// The id (as a string) of the singer that was active when the app last closed, so the same singer's lists
    /// re-open on the next launch. Empty when never set; the bootstrap falls back to the first singer when it's
    /// empty or names a singer that no longer exists. String-typed to sit in the same key/value store as the rest.
    /// </summary>
    string LastActiveSingerId { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on YouTube" button.</summary>
    bool YouTubeSearchEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on Spotify" button.</summary>
    bool SpotifySearchEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on KaraFun" button (needs <see cref="KaraFunVenueId"/>).</summary>
    bool KaraFunEnabled { get; set; }

    /// <summary>
    /// One-time guard for the legacy <see cref="KaraFunVenueId"/> → seeded-venue migration. Defaults to <c>false</c>
    /// (like <see cref="TutorialCompleted"/>): the first time the venue feature loads with an old global KaraFun ID
    /// set and no saved venues, one venue is seeded from it and this flips to <c>true</c> so it never re-seeds.
    /// </summary>
    bool VenuesSeeded { get; set; }

    /// <summary>
    /// When true, the active venue is auto-selected from the device's location — switching to the nearest saved
    /// venue as the singer moves, and re-checking every <see cref="VenueRecheckMinutes"/> minutes while the app is
    /// open (foreground only). Defaults to <c>true</c>; the location permission is requested the first time it runs.
    /// Manually picking a venue pins it until "resume auto-detect"; turn this off to switch venues only by hand.
    /// </summary>
    bool LocationAutoDetect { get; set; }

    /// <summary>
    /// How often (minutes) the location auto-detect re-checks the current venue while the app is open. Defaults to
    /// 5; the UI offers a small set of choices and the tracker clamps to ~2–30.
    /// </summary>
    int VenueRecheckMinutes { get; set; }

    /// <summary>
    /// Legacy single global KaraFun venue ID. KaraFun is now per-venue (each <c>Venue</c> carries its own ID and the
    /// "Find on KaraFun" button follows the active venue), so this is no longer set from the UI — it's kept only so
    /// the one-time upgrade migration can seed a venue from an older install's value.
    /// </summary>
    string KaraFunVenueId { get; set; }

    /// <summary>When true, the song detail sheet shows the "Lyrics" button (looks lyrics up from LRCLIB).</summary>
    bool LyricsEnabled { get; set; }

    /// <summary>
    /// When true, looked-up lyrics are cached on-device so re-opening a song's lyrics skips the network. When
    /// false, every open re-fetches from LRCLIB (and nothing new is written to the cache).
    /// </summary>
    bool LyricsCacheEnabled { get; set; }

    /// <summary>When true, tapping a song's favorite star scrolls the list to reveal that song's new position.</summary>
    bool ScrollToFavorited { get; set; }

    /// <summary>
    /// When true, a song's free-form tags are shown as chips on its card and detail sheet, the Tags filter is
    /// offered on My Songs, and the tag inputs appear on the add / edit forms. When false, all of that is hidden;
    /// any tags already saved on songs are left untouched, so turning it back on restores them.
    /// </summary>
    bool TagsEnabled { get; set; }

    /// <summary>
    /// When true, each song's cover art (fetched from iTunes and cached on-device) is shown as its card background
    /// and behind the title on its detail sheet, with a scrim behind the text for legibility. Defaults to
    /// <c>true</c> like the other feature flags; turn it off to keep plain cards and skip the cover lookups/downloads.
    /// </summary>
    bool AlbumArtEnabled { get; set; }

    /// <summary>When true, the "Surprise me" random-picker button is shown on My Songs. When false it's hidden.</summary>
    bool SurpriseEnabled { get; set; }

    /// <summary>
    /// When true, the "Surprise me" random picker skips any song already sung today, so it suggests something
    /// fresh. If every candidate has already been sung today it falls back to the full list rather than doing
    /// nothing.
    /// </summary>
    bool SurpriseSkipSungToday { get; set; }

    /// <summary>
    /// When true, marking a song sung shows a "how it went" star rating on the prompt. Turn off for singers who'd
    /// rather just log the performance (and jot a note) without judging it — the prompt then only asks for a note.
    /// </summary>
    bool RatePerformances { get; set; }

    /// <summary>
    /// When true, a song's how-it-went star weights recent sings more than old ones (an exponential time-decay), so
    /// what you're crushing lately ranks above what you nailed years ago. When false (the default), every rated sing
    /// counts equally. Affects only the derived star + list ranking on My Songs — the stored performances are untouched.
    /// </summary>
    bool RecencyWeightedRatings { get; set; }

    /// <summary>
    /// When true, the app checks GitHub for a newer release once at startup and shows a banner when one is
    /// available. When false, no update check runs (no network request) and the banner never appears.
    /// </summary>
    bool UpdateCheckEnabled { get; set; }

    /// <summary>
    /// Whether the first-run tutorial has been completed or skipped. Unlike the feature flags above this defaults
    /// to <c>false</c> (opposite semantics), so the coach-marks overlay shows once on a fresh install and never
    /// again after. Set back to <c>false</c> (from Settings → "Replay tutorial") to see the tour again.
    /// </summary>
    bool TutorialCompleted { get; set; }

    /// <summary>
    /// Comma-joined ids of the user's OWN songs the tutorial queued onto an empty Tonight set (the tour's
    /// no-seeded-songs path), persisted so a tour interrupted by an app kill can still remove exactly those rows
    /// on its next run — the in-memory tracking list doesn't survive a restart, and those rows aren't the fixed-id
    /// samples the self-heal already knows. Empty when no tour seeding is outstanding.
    /// </summary>
    string TutorialSeededTonightIds { get; set; }
}
