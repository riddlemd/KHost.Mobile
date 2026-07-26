using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by MAUI <see cref="Preferences"/>, the per-app key/value store that persists across launches.
/// Reads/writes are synchronous.
/// </remarks>
public sealed class MauiAppSettings : IAppSettings
{
    private const string AutoFillMetadataKey = "settings.autofill_metadata";
    private const string TonightKey = "settings.tonight";
    private const string SurpriseKey = "settings.surprise";
    private const string YouTubeSearchKey = "settings.youtube_search";
    private const string SpotifySearchKey = "settings.spotify_search";
    private const string KaraFunKey = "settings.karafun";
    private const string KaraFunVenueIdKey = "settings.karafun_venue_id";
    private const string VenuesSeededKey = "settings.venues_seeded";
    private const string LocationAutoDetectKey = "settings.location_autodetect";
    private const string VenueRecheckMinutesKey = "settings.venue_recheck_minutes";
    private const string LyricsKey = "settings.lyrics";
    private const string LyricsCacheKey = "settings.lyrics_cache";
    private const string ScrollToFavoritedKey = "settings.scroll_to_favorited";
    private const string TagsKey = "settings.tags";
    private const string AlbumArtKey = "settings.album_art";
    private const string SurpriseSkipSungTodayKey = "settings.surprise_skip_sung_today";
    private const string SurpriseFavourWellSungKey = "settings.surprise_favour_well_sung";
    private const string SurpriseNeverSungOnlyKey = "settings.surprise_never_sung_only";
    private const string SurpriseFavoritesOnlyKey = "settings.surprise_favorites_only";
    private const string SurpriseRespectFiltersKey = "settings.surprise_respect_filters";
    private const string RatePerformancesKey = "settings.rate_performances";
    private const string RecencyWeightedRatingsKey = "settings.recency_weighted_ratings";
    private const string UpdateCheckKey = "settings.update_check";
    private const string TutorialCompletedKey = "settings.tutorial_completed";
    private const string TutorialSeededTonightIdsKey = "settings.tutorial_seeded_tonight_ids";
    private const string LastActiveSingerIdKey = "settings.last_active_singer_id";
    private const string HapticsKey = "settings.haptics";
    private const string Use24HourTimeKey = "settings.use_24h_time";
    private const string FloatFavoritesKey = "settings.float_favorites";
    private const string ConfirmSongDeleteKey = "settings.confirm_song_delete";
    private const string UndoWindowSecondsKey = "settings.undo_window_seconds";
    private const string LaunchDestinationKey = "settings.launch_destination";
    private const string RecencyHalfLifeDaysKey = "settings.recency_half_life_days";
    private const string VenueDetectionMetersKey = "settings.venue_detection_meters";
    private const string ShowDistanceInFeetKey = "settings.show_distance_in_feet";
    private const string RatingPriorWeightKey = "settings.rating_prior_weight";
    private const string VenueHistoryEntriesKey = "settings.venue_history_entries";
    private const string SpellingSuggestionLevelKey = "settings.spelling_suggestion_level";
    private const string CatalogueRegionKey = "settings.catalogue_region";
    private const string ImportLookupDelayMsKey = "settings.import_lookup_delay_ms";

    public bool AutoFillMetadata
    {
        get => Preferences.Default.Get(AutoFillMetadataKey, true);
        set => Preferences.Default.Set(AutoFillMetadataKey, value);
    }

    public bool TonightEnabled
    {
        get => Preferences.Default.Get(TonightKey, true);
        set => Preferences.Default.Set(TonightKey, value);
    }

    public bool SurpriseEnabled
    {
        get => Preferences.Default.Get(SurpriseKey, true);
        set => Preferences.Default.Set(SurpriseKey, value);
    }

    // Empty default means "no remembered singer yet"; the bootstrap then picks the first singer.
    public string LastActiveSingerId
    {
        get => Preferences.Default.Get(LastActiveSingerIdKey, string.Empty);
        set => Preferences.Default.Set(LastActiveSingerIdKey, value);
    }

    public bool YouTubeSearchEnabled
    {
        get => Preferences.Default.Get(YouTubeSearchKey, true);
        set => Preferences.Default.Set(YouTubeSearchKey, value);
    }

    public bool SpotifySearchEnabled
    {
        get => Preferences.Default.Get(SpotifySearchKey, true);
        set => Preferences.Default.Set(SpotifySearchKey, value);
    }

    public bool KaraFunEnabled
    {
        get => Preferences.Default.Get(KaraFunKey, true);
        set => Preferences.Default.Set(KaraFunKey, value);
    }

    // Empty string default means "no venue set yet".
    public string KaraFunVenueId
    {
        get => Preferences.Default.Get(KaraFunVenueIdKey, string.Empty);
        set => Preferences.Default.Set(KaraFunVenueIdKey, value);
    }

    // Defaults to false (not true like the feature flags): a fresh install hasn't run the legacy-ID migration yet.
    public bool VenuesSeeded
    {
        get => Preferences.Default.Get(VenuesSeededKey, false);
        set => Preferences.Default.Set(VenuesSeededKey, value);
    }

    public bool LocationAutoDetect
    {
        get => Preferences.Default.Get(LocationAutoDetectKey, true);
        set => Preferences.Default.Set(LocationAutoDetectKey, value);
    }

    public int VenueRecheckMinutes
    {
        get => Preferences.Default.Get(VenueRecheckMinutesKey, 5);
        set => Preferences.Default.Set(VenueRecheckMinutesKey, value);
    }

    public bool LyricsEnabled
    {
        get => Preferences.Default.Get(LyricsKey, true);
        set => Preferences.Default.Set(LyricsKey, value);
    }

    public bool LyricsCacheEnabled
    {
        get => Preferences.Default.Get(LyricsCacheKey, true);
        set => Preferences.Default.Set(LyricsCacheKey, value);
    }

    public bool ScrollToFavorited
    {
        get => Preferences.Default.Get(ScrollToFavoritedKey, true);
        set => Preferences.Default.Set(ScrollToFavoritedKey, value);
    }

    public bool TagsEnabled
    {
        get => Preferences.Default.Get(TagsKey, true);
        set => Preferences.Default.Set(TagsKey, value);
    }

    public bool AlbumArtEnabled
    {
        get => Preferences.Default.Get(AlbumArtKey, true);
        set => Preferences.Default.Set(AlbumArtKey, value);
    }

    public bool SurpriseSkipSungToday
    {
        get => Preferences.Default.Get(SurpriseSkipSungTodayKey, true);
        set => Preferences.Default.Set(SurpriseSkipSungTodayKey, value);
    }

    public bool SurpriseFavourWellSung
    {
        get => Preferences.Default.Get(SurpriseFavourWellSungKey, true);
        set => Preferences.Default.Set(SurpriseFavourWellSungKey, value);
    }

    public bool SurpriseNeverSungOnly
    {
        get => Preferences.Default.Get(SurpriseNeverSungOnlyKey, false);
        set => Preferences.Default.Set(SurpriseNeverSungOnlyKey, value);
    }

    public bool SurpriseFavoritesOnly
    {
        get => Preferences.Default.Get(SurpriseFavoritesOnlyKey, false);
        set => Preferences.Default.Set(SurpriseFavoritesOnlyKey, value);
    }

    public bool SurpriseRespectFilters
    {
        get => Preferences.Default.Get(SurpriseRespectFiltersKey, true);
        set => Preferences.Default.Set(SurpriseRespectFiltersKey, value);
    }

    public bool RatePerformances
    {
        get => Preferences.Default.Get(RatePerformancesKey, true);
        set => Preferences.Default.Set(RatePerformancesKey, value);
    }

    // Defaults to false (not true like most flags): the plain equal-weight average is the baseline behavior, and
    // recency weighting is an opt-in for singers who want their recent form to lead.
    public bool RecencyWeightedRatings
    {
        get => Preferences.Default.Get(RecencyWeightedRatingsKey, false);
        set => Preferences.Default.Set(RecencyWeightedRatingsKey, value);
    }

    public bool UpdateCheckEnabled
    {
        get => Preferences.Default.Get(UpdateCheckKey, true);
        set => Preferences.Default.Set(UpdateCheckKey, value);
    }

    // Defaults to false (not true like the flags above): a fresh install has NOT seen the tutorial, so it shows.
    public bool TutorialCompleted
    {
        get => Preferences.Default.Get(TutorialCompletedKey, false);
        set => Preferences.Default.Set(TutorialCompletedKey, value);
    }

    public string TutorialSeededTonightIds
    {
        get => Preferences.Default.Get(TutorialSeededTonightIdsKey, string.Empty);
        set => Preferences.Default.Set(TutorialSeededTonightIdsKey, value);
    }

    public bool HapticsEnabled
    {
        get => Preferences.Default.Get(HapticsKey, true);
        set => Preferences.Default.Set(HapticsKey, value);
    }

    public bool Use24HourTime
    {
        get => Preferences.Default.Get(Use24HourTimeKey, false);
        set => Preferences.Default.Set(Use24HourTimeKey, value);
    }

    public bool FloatFavoritesToTop
    {
        get => Preferences.Default.Get(FloatFavoritesKey, true);
        set => Preferences.Default.Set(FloatFavoritesKey, value);
    }

    // Defaults to false: removal has always been swipe-then-undo, and the undo window is the safety net.
    public bool ConfirmSongDelete
    {
        get => Preferences.Default.Get(ConfirmSongDeleteKey, false);
        set => Preferences.Default.Set(ConfirmSongDeleteKey, value);
    }

    public int UndoWindowSeconds
    {
        get => Preferences.Default.Get(UndoWindowSecondsKey, 5);
        set => Preferences.Default.Set(UndoWindowSecondsKey, value);
    }

    public string LaunchDestination
    {
        get => Preferences.Default.Get(LaunchDestinationKey, "smart");
        set => Preferences.Default.Set(LaunchDestinationKey, value);
    }

    public int RecencyHalfLifeDays
    {
        get => Preferences.Default.Get(RecencyHalfLifeDaysKey, 180);
        set => Preferences.Default.Set(RecencyHalfLifeDaysKey, value);
    }

    public int VenueDetectionMeters
    {
        get => Preferences.Default.Get(VenueDetectionMetersKey, 75);
        set => Preferences.Default.Set(VenueDetectionMetersKey, value);
    }

    public bool ShowDistanceInFeet
    {
        get => Preferences.Default.Get(ShowDistanceInFeetKey, true);
        set => Preferences.Default.Set(ShowDistanceInFeetKey, value);
    }

    public int RatingPriorWeight
    {
        get => Preferences.Default.Get(RatingPriorWeightKey, 3);
        set => Preferences.Default.Set(RatingPriorWeightKey, value);
    }

    public int VenueHistoryEntries
    {
        get => Preferences.Default.Get(VenueHistoryEntriesKey, 5);
        set => Preferences.Default.Set(VenueHistoryEntriesKey, value);
    }

    // 1 = cautious, the thresholds the matcher shipped with.
    public int SpellingSuggestionLevel
    {
        get => Preferences.Default.Get(SpellingSuggestionLevelKey, 1);
        set => Preferences.Default.Set(SpellingSuggestionLevelKey, value);
    }

    public string CatalogueRegion
    {
        get => Preferences.Default.Get(CatalogueRegionKey, "US");
        set => Preferences.Default.Set(CatalogueRegionKey, value);
    }

    public int ImportLookupDelayMs
    {
        get => Preferences.Default.Get(ImportLookupDelayMsKey, 3000);
        set => Preferences.Default.Set(ImportLookupDelayMsKey, value);
    }
}
