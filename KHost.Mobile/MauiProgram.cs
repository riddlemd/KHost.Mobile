using KHost.Mobile.Client.Enrichment;
using KHost.Mobile.Client.Lyrics;
using KHost.Mobile.Client.Spotify;
using KHost.Mobile.Client.Updates;
using KHost.Mobile.Client.YouTubeMusic;
using KHost.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace KHost.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // On-device song list. Singleton so its in-memory cache and Changed event are shared app-wide. The UI binds
        // to ISongListStore; the concrete type is also registered so both resolve to the one instance.
        builder.Services.AddSingleton<JsonFileSongListStore>();
        builder.Services.AddSingleton<ISongListStore>(sp => sp.GetRequiredService<JsonFileSongListStore>());

        // "Tonight" on-deck set list — its own JSON-file store (separate from the song list). Singleton so its
        // in-memory set + Changed event are shared app-wide.
        builder.Services.AddSingleton<ITonightStore, JsonFileTonightStore>();

        // User preferences (feature toggles) persisted via MAUI Preferences. Singleton: one view of the flags app-wide.
        builder.Services.AddSingleton<IAppSettings, MauiAppSettings>();

        // Ephemeral per-launch state (e.g. the one-time smart-landing decision). Singleton; not persisted.
        builder.Services.AddSingleton<IAppSession, AppSession>();

        // On-device lyrics cache (JSON file). Singleton so its in-memory map + Changed event are shared app-wide.
        builder.Services.AddSingleton<ILyricsCache, JsonFileLyricsCache>();

        // Opens external links (e.g. a YouTube search) in the OS browser / matching app.
        builder.Services.AddSingleton<ILinkLauncher, MauiLinkLauncher>();

        // HTTP-backed services go through IHttpClientFactory (AddHttpClient) so their message handlers are
        // pooled and rotated — a plain long-lived `new HttpClient()` never picks up DNS changes. Each service
        // is stateless (only const fields) and consumed via @inject, so the typed client's transient lifetime
        // is fine. The two that need a browser User-Agent set it per-request, so no config lambda is needed.

        // Token-free import of public YouTube Music playlists (title + artist) via the playlist page.
        builder.Services.AddHttpClient<IYouTubeMusicImportService, YouTubeMusicImportService>();

        // Token-free import of public Spotify playlists (title + artist) via the embed endpoint.
        builder.Services.AddHttpClient<ISpotifyImportService, SpotifyImportService>();

        // Keyless release-year + genre lookup (iTunes Search API). Re-lookup is avoided per-song via the
        // SongListItem.MetadataLookedUp flag, so no separate cache layer is needed.
        builder.Services.AddHttpClient<ITrackMetadataLookup, ITunesTrackMetadataLookup>();

        // Keyless lyrics lookup (LRCLIB). Base address + the descriptive User-Agent LRCLIB's fair-use policy
        // asks for are set here (the client library stays MAUI-free, so the app version is injected at this seam).
        builder.Services.AddHttpClient<ILyricsClient, LrcLibLyricsClient>(http =>
        {
            http.BaseAddress = new Uri("https://lrclib.net/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"KHost.Mobile/{AppInfo.Current.VersionString} (+https://github.com/riddlemd/KHost.Mobile)");
        });

        // "Update available" check — reads this repo's GitHub Releases feed. GitHub's REST API requires a
        // User-Agent; the Accept header pins the v3 media type. Orchestrated by IAppUpdateService (which owns
        // the version compare, the setting gate, and the once-per-launch memoization).
        builder.Services.AddHttpClient<IUpdateClient, GitHubReleaseClient>(http =>
        {
            http.BaseAddress = new Uri("https://api.github.com/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"KHost.Mobile/{AppInfo.Current.VersionString} (+https://github.com/riddlemd/KHost.Mobile)");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        builder.Services.AddSingleton<IAppUpdateService, MauiAppUpdateService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
