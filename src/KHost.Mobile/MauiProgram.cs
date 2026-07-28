using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Clients;
using KHost.Mobile.Infrastructure;
using KHost.Mobile.Diagnostics;
using KHost.Mobile.Infrastructure.Diagnostics;
using KHost.Mobile.Infrastructure.Services;
using KHost.Mobile.Services;
using KHost.Mobile.UI;
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

        // Each project registers what it implements; the concrete types are internal to them, so this file
        // names only interfaces and the platform adapters it owns.
        builder.Services.AddKHostInfrastructure();
        builder.Services.AddKHostUI();

        // The MAUI-bound adapters — the only implementations this head supplies itself.
        builder.Services.AddSingleton<ILocationProvider, MauiLocationProvider>();
        builder.Services.AddSingleton<IAppSettings, MauiAppSettings>();
        builder.Services.AddSingleton<ILinkLauncher, MauiLinkLauncher>();
        builder.Services.AddSingleton<IAppVersionInfo, MauiAppVersionInfo>();
        builder.Services.AddSingleton<IFileExchange, MauiFileExchange>();
        builder.Services.AddSingleton<IHaptics, MauiHaptics>();

        // Platform-conditional, so it can't move into Infrastructure: native on Android/iOS, a no-op elsewhere
        // (the scan button hides there, but IQrScanner still has to resolve).
#if ANDROID || IOS
        builder.Services.AddSingleton<IQrScanner, MauiQrScanner>();
#else
        builder.Services.AddSingleton<IQrScanner, UnsupportedQrScanner>();
#endif

        // The version is the head's to know; Clients takes it as the User-Agent LRCLIB asks for and GitHub
        // requires. The logging handler is chained here too — the library itself does no logging.
        builder.Services.AddKHostClients(
            $"KHost.Mobile/{AppInfo.Current.VersionString} (+https://github.com/riddlemd/KHost.Mobile)",
            http => http.AddHttpMessageHandler<LoggingHttpMessageHandler>());

        // Cover-image downloads for IAlbumArtCache — the artwork CDN, not the rate-limited Search API, so a plain
        // named client is enough; the short timeout keeps a slow image from hanging a best-effort load.
        builder.Services.AddHttpClient("album-art", http => http.Timeout = TimeSpan.FromSeconds(20))
            .AddHttpMessageHandler<LoggingHttpMessageHandler>();

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
