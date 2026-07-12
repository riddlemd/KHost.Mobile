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
    /// When true, the "Tonight" on-deck set list is available: it's pinned atop My Songs and every song gets a
    /// quick "add to tonight" control. When false, the Tonight section and its controls are hidden (the saved set
    /// is left untouched, so turning it back on restores it).
    /// </summary>
    bool TonightEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on YouTube" button.</summary>
    bool YouTubeSearchEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on Spotify" button.</summary>
    bool SpotifySearchEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Lyrics" button (looks lyrics up from LRCLIB).</summary>
    bool LyricsEnabled { get; set; }

    /// <summary>
    /// When true, looked-up lyrics are cached on-device so re-opening a song's lyrics skips the network. When
    /// false, every open re-fetches from LRCLIB (and nothing new is written to the cache).
    /// </summary>
    bool LyricsCacheEnabled { get; set; }

    /// <summary>When true, tapping a song's favorite star scrolls the list to reveal that song's new position.</summary>
    bool ScrollToFavorited { get; set; }

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
}
