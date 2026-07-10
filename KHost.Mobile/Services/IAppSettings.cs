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

    /// <summary>When true, the song detail sheet shows the "Find on YouTube" button.</summary>
    bool YouTubeSearchEnabled { get; set; }

    /// <summary>When true, the song detail sheet shows the "Find on Spotify" button.</summary>
    bool SpotifySearchEnabled { get; set; }

    /// <summary>When true, tapping a song's favorite star scrolls the list to reveal that song's new position.</summary>
    bool ScrollToFavorited { get; set; }
}
