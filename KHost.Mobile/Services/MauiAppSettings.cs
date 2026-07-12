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
    private const string YouTubeSearchKey = "settings.youtube_search";
    private const string SpotifySearchKey = "settings.spotify_search";
    private const string ScrollToFavoritedKey = "settings.scroll_to_favorited";
    private const string SurpriseSkipSungTodayKey = "settings.surprise_skip_sung_today";
    private const string RatePerformancesKey = "settings.rate_performances";

    public bool AutoFillMetadata
    {
        get => Preferences.Default.Get(AutoFillMetadataKey, true);
        set => Preferences.Default.Set(AutoFillMetadataKey, value);
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

    public bool ScrollToFavorited
    {
        get => Preferences.Default.Get(ScrollToFavoritedKey, true);
        set => Preferences.Default.Set(ScrollToFavoritedKey, value);
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
}
