using KHost.Mobile.Abstractions.Services;

namespace KHost.Mobile.UnitTests;

// Every settings-dependent behaviour reads IAppSettings, which is 39 tunables wide — implementing it inline
// per test would bury the two values a test actually cares about. Construct with an object initializer and
// set only those; the rest sit at their type default, which is NOT the app's default and is not meant to be.
// A test that depends on a real default should say so explicitly.
internal sealed class FakeAppSettings : IAppSettings
{
    public bool AutoFillMetadata { get; set; }
    public bool TonightEnabled { get; set; }
    public string LastActiveSingerId { get; set; } = "";
    public bool YouTubeSearchEnabled { get; set; }
    public bool SpotifySearchEnabled { get; set; }
    public bool KaraFunFeaturesEnabled { get; set; }
    public bool LocationAutoDetect { get; set; }
    public int VenueRecheckMinutes { get; set; }
    public bool LyricsEnabled { get; set; }
    public bool LyricsCacheEnabled { get; set; }
    public bool ScrollToFavorited { get; set; }
    public bool TagsEnabled { get; set; }
    public bool AlbumArtEnabled { get; set; }
    public bool SurpriseEnabled { get; set; }
    public bool SurpriseSkipSungToday { get; set; }
    public bool SurpriseFavourWellSung { get; set; }
    public bool SurpriseNeverSungOnly { get; set; }
    public bool SurpriseFavoritesOnly { get; set; }
    public bool SurpriseRespectFilters { get; set; }
    public bool RatePerformances { get; set; }
    public bool RecencyWeightedRatings { get; set; }
    public bool UpdateCheckEnabled { get; set; }
    public bool TutorialCompleted { get; set; }
    public string TutorialSeededTonightIds { get; set; } = "";
    public bool HapticsEnabled { get; set; }
    public bool Use24HourTime { get; set; }
    public bool FloatFavoritesToTop { get; set; }
    public bool ConfirmSongDelete { get; set; }
    public int UndoWindowSeconds { get; set; }
    public string LaunchDestination { get; set; } = "";
    public int RecencyHalfLifeDays { get; set; }
    public int VenueDetectionMeters { get; set; }
    public bool ShowDistanceInFeet { get; set; }
    public int RatingPriorWeight { get; set; }
    public int VenueHistoryEntries { get; set; }
    public int SpellingSuggestionLevel { get; set; }
    public string CatalogueRegion { get; set; } = "";
    public int ImportLookupDelayMs { get; set; }
}
