using KHost.Mobile.Clients.Deezer;
using KHost.Mobile.Clients.Enrichment;
using KHost.Mobile.Clients.Lyrics;
using KHost.Mobile.Clients.Spotify;
using KHost.Mobile.Clients.Updates;
using KHost.Mobile.Clients.YouTubeMusic;
using KHost.Mobile.Diagnostics;
using KHost.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
#if ANDROID || IOS
using BarcodeScanning;
#endif

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

#if ANDROID || IOS
        // Registers the native barcode scanner (ML Kit / Apple Vision) used to scan a KaraFun venue QR code.
        builder.UseBarcodeScanning();
#endif

        // The private data folder the JSON stores write into. Abstracted behind IAppDataDirectory so the stores stay
        // MAUI-free and unit-testable; the app binds it to the real FileSystem.AppDataDirectory.
        builder.Services.AddSingleton<IAppDataDirectory, MauiAppDataDirectory>();

        // Singleton so the in-memory cache and Changed event are shared app-wide. The concrete type is registered
        // too, so both it and ISongListStore resolve to the one instance.
        builder.Services.AddSingleton<JsonFileSongListStore>();
        builder.Services.AddSingleton<ISongListStore>(sp => sp.GetRequiredService<JsonFileSongListStore>());

        // "Tonight" on-deck set list — its own JSON-file store, deliberately separate from the song list.
        builder.Services.AddSingleton<ITonightStore, JsonFileTonightStore>();

        // The saved venues. The ACTIVE venue is not persisted here — it's an ephemeral pointer on IAppSession.
        builder.Services.AddSingleton<IVenueStore, JsonFileVenueStore>();

        // Roster of singers who share this device. Like the venue, the ACTIVE singer lives on IAppSession — the
        // per-singer song/tonight stores above read it from there to pick each singer's file.
        builder.Services.AddSingleton<ISingerStore, JsonFileSingerStore>();

        // Device location + the venue auto-selector behind it. ILocationProvider wraps MAUI Geolocation (best-effort,
        // permission-gated); IVenueLocator turns a fix into the nearest saved venue and sets it active. Both singleton.
        builder.Services.AddSingleton<ILocationProvider, MauiLocationProvider>();
        builder.Services.AddSingleton<IVenueLocator, MauiVenueLocator>();

        // User preferences (feature toggles) persisted via MAUI Preferences. Singleton: one view of the flags app-wide.
        builder.Services.AddSingleton<IAppSettings, MauiAppSettings>();

        // Ephemeral per-launch state (e.g. the one-time smart-landing decision). Singleton; not persisted.
        builder.Services.AddSingleton<IAppSession, AppSession>();

        // On-device lyrics cache (JSON file). Singleton so its in-memory map + Changed event are shared app-wide.
        builder.Services.AddSingleton<ILyricsCache, JsonFileLyricsCache>();

        // On-device album-art cache (image blobs); pulls a pooled HttpClient from the factory per download.
        builder.Services.AddSingleton<IAlbumArtCache, AlbumArtCache>();
        // SCOPED, not singleton — it talks to the WebView through IJSRuntime, which is itself scoped; a singleton
        // captures a JS runtime that isn't attached to this WebView and every transfer silently fails. Blazor
        // Hybrid has one scope per WebView, so scoped is still app-wide (and shared by My Songs + Tonight).
        builder.Services.AddScoped<IAlbumArtService, AlbumArtService>();

        builder.Services.AddSingleton<ILinkLauncher, MauiLinkLauncher>();

        builder.Services.AddSingleton<IHaptics, MauiHaptics>();

        // Safe-area insets for platforms whose WebView can't see the system bars via CSS env() (Android).
        // Singleton so the activity's inset listener and MainLayout share one instance.
        builder.Services.AddSingleton<ISafeAreaInsets, SafeAreaInsets>();

        // Overlay registry the Android back button consults. Singleton so the components and the platform
        // callback share one instance.
        builder.Services.AddSingleton<IBackButtonService, BackButtonService>();

        // QR scanner for the KaraFun venue link. Native (ML Kit / Apple Vision) on Android/iOS; a no-op stub on
        // other heads (the scan button is hidden there, but the KaraFun sheet still resolves IQrScanner via DI).
#if ANDROID || IOS
        builder.Services.AddSingleton<IQrScanner, MauiQrScanner>();
#else
        builder.Services.AddSingleton<IQrScanner, UnsupportedQrScanner>();
#endif

        // HTTP-backed services go through IHttpClientFactory so their message handlers are pooled and rotated —
        // a plain long-lived `new HttpClient()` never picks up DNS changes. Every client below chains
        // LoggingHttpMessageHandler so its requests/responses are logged at one seam.
        builder.Services.AddTransient<LoggingHttpMessageHandler>();

        // Token-free import of public YouTube Music playlists (title + artist) via the playlist page.
        builder.Services.AddHttpClient<IYouTubeMusicImportService, YouTubeMusicImportService>()
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Token-free import of public Spotify playlists (title + artist) via the embed endpoint.
        builder.Services.AddHttpClient<ISpotifyImportService, SpotifyImportService>()
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Keyless release-year + genre + cover-art-URL lookup (iTunes Search API). Re-lookup is avoided per-song
        // via the SongListItem.MetadataLookedUp / ArtworkLookedUp flags, so no separate cache layer is needed.
        builder.Services.AddHttpClient<ITrackMetadataLookup, ITunesTrackMetadataLookup>()
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Keyless cover-art FALLBACK (Deezer). Consulted only when iTunes finds no cover — Deezer's field-scoped
        // search surfaces tracks iTunes' popularity-ranked search misses (e.g. album deep cuts). Art only; its
        // release dates are unreliable, so year/genre stay with iTunes.
        builder.Services.AddHttpClient<ICoverArtLookup, DeezerCoverArtLookup>()
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Keyless spelling-correction FALLBACK (Deezer again, but the plain free-text search — the field-scoped
        // one above is exact-only and returns nothing for a typo). Consulted only when iTunes offered neither a
        // match nor a correction.
        builder.Services.AddHttpClient<ISpellingSuggestionLookup, DeezerSpellingSuggestionLookup>()
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Cover-image downloads for IAlbumArtCache. Hits the artwork CDN (not the rate-limited Search API), so a
        // plain named client is enough; a short timeout keeps a slow image from hanging the best-effort load.
        builder.Services.AddHttpClient("album-art", http => http.Timeout = TimeSpan.FromSeconds(20))
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // Keyless lyrics lookup (LRCLIB). Base address + the descriptive User-Agent LRCLIB's fair-use policy
        // asks for are set here (the client library stays MAUI-free, so the app version is injected at this seam).
        builder.Services.AddHttpClient<ILyricsClient, LrcLibLyricsClient>(http =>
        {
            http.BaseAddress = new Uri("https://lrclib.net/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"KHost.Mobile/{AppInfo.Current.VersionString} (+https://github.com/riddlemd/KHost.Mobile)");
        }).AddHttpMessageHandler<LoggingHttpMessageHandler>();

        // "Update available" check — reads this repo's GitHub Releases feed. GitHub's REST API rejects a request
        // with no User-Agent; the Accept header pins the v3 media type.
        builder.Services.AddHttpClient<IUpdateClient, GitHubReleaseClient>(http =>
        {
            http.BaseAddress = new Uri("https://api.github.com/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"KHost.Mobile/{AppInfo.Current.VersionString} (+https://github.com/riddlemd/KHost.Mobile)");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }).AddHttpMessageHandler<LoggingHttpMessageHandler>();
        builder.Services.AddSingleton<IAppUpdateService, MauiAppUpdateService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#if ANDROID
        // AddDebug() alone goes nowhere on a debugger-less `-t:Run` deploy; this routes ILogger to logcat under the
        // "KHostCue" tag (adb logcat -s KHostCue) so the app's own diagnostics are actually visible on-device.
        builder.Logging.AddProvider(new AndroidLogcatLoggerProvider());
#endif
        // App diagnostics at Debug, framework chatter at Warning so they stand out. Debug-build only.
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("KHost.Mobile", LogLevel.Debug);
#endif

        return builder.Build();
    }
}
