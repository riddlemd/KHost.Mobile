using KHost.Mobile.Abstractions.Services;

namespace KHost.Mobile.IntegrationTests;

// IAppSettings is dozens of tunables wide — implementing it inline per test would bury the one value a test cares about.
// Set only that with an object initializer; the rest sit at their type default, which is NOT the app's default.
internal sealed class StubSettings : IAppSettings
{
    public bool AutoFillMetadata { get; set; }
    public bool SongSearchEnabled { get; set; }
    public bool TonightEnabled { get; set; }
    public int TonightExpiryHours { get; set; }
    public string LastActiveSingerId { get; set; } = "";
    public string LastActiveVenueId { get; set; } = "";
    public bool LastActiveVenuePinned { get; set; }
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
