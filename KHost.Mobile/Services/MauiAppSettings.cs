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
}
