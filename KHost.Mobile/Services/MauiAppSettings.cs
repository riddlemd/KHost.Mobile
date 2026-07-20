using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IAppSettings"/> backed by MAUI <see cref="Preferences"/> — the per-app key/value store that
/// persists across launches. Reads/writes are synchronous; each flag defaults to <c>true</c> so a fresh install
/// (or a key that was never written) keeps the original behavior.
/// </summary>
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
    private const string RatePerformancesKey = "settings.rate_performances";
    private const string UpdateCheckKey = "settings.update_check";
    private const string TutorialCompletedKey = "settings.tutorial_completed";
    private const string TutorialSeededTonightIdsKey = "settings.tutorial_seeded_tonight_ids";
    private const string LastActiveSingerIdKey = "settings.last_active_singer_id";

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

    public bool RatePerformances
    {
        get => Preferences.Default.Get(RatePerformancesKey, true);
        set => Preferences.Default.Set(RatePerformancesKey, value);
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
}
