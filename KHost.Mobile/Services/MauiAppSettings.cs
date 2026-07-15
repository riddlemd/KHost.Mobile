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
    private const string LyricsKey = "settings.lyrics";
    private const string LyricsCacheKey = "settings.lyrics_cache";
    private const string ScrollToFavoritedKey = "settings.scroll_to_favorited";
    private const string TagsKey = "settings.tags";
    private const string AlbumArtKey = "settings.album_art";
    private const string SurpriseSkipSungTodayKey = "settings.surprise_skip_sung_today";
    private const string RatePerformancesKey = "settings.rate_performances";
    private const string UpdateCheckKey = "settings.update_check";
    private const string TutorialCompletedKey = "settings.tutorial_completed";

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

    // The first string-valued setting; empty string default means "no venue set yet".
    public string KaraFunVenueId
    {
        get => Preferences.Default.Get(KaraFunVenueIdKey, string.Empty);
        set => Preferences.Default.Set(KaraFunVenueIdKey, value);
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
}
