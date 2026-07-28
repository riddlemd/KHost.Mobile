using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Diagnostics;
using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KHost.Mobile.Infrastructure;

/// <summary>
/// Registers everything this project implements. The concrete types are <c>internal</c>, so this is the only way
/// to get at them — a consumer names the interface and never the implementation.
/// </summary>
public static class Project
{
    /// <summary>
    /// Adds the JSON stores, session, caches and the MAUI-free platform-policy services.
    /// </summary>
    /// <remarks>
    /// <see cref="IAppDataDirectory"/> is NOT registered here — it resolves a device path, so only the head knows
    /// it. Neither is <see cref="IQrScanner"/>: its fallback is platform-conditional, and registering it here
    /// would race the head's native one.
    /// </remarks>
    public static IServiceCollection AddKHostInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Injected, never ambient: a test freezes the clock and seeds the RNG through these.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);

        // Registered twice on purpose — the profile export/import path resolves the concrete store, and every
        // other consumer the interface. One instance, so they share a cache; collapsing it splits them.
        services.AddSingleton<JsonFileSongListStore>();
        services.AddSingleton<ISongListStore>(sp => sp.GetRequiredService<JsonFileSongListStore>());

        services.AddSingleton<ITonightStore, JsonFileTonightStore>();
        services.AddSingleton<IVenueStore, JsonFileVenueStore>();
        services.AddSingleton<ISingerStore, JsonFileSingerStore>();
        services.AddSingleton<ILyricsCache, JsonFileLyricsCache>();

        services.AddSingleton<IAppSession, AppSession>();
        services.AddSingleton<IVenueLocator, VenueLocator>();
        services.AddSingleton<IAlbumArtCache, AlbumArtCache>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<ISafeAreaInsets, SafeAreaInsets>();
        services.AddSingleton<IBackButtonService, BackButtonService>();

        // Feeds the settings-backed catalogue region to Clients, which can't read IAppSettings itself.
        services.AddSingleton<ILookupOptions, AppLookupOptions>();

        // Chained onto every typed client so one seam logs what the native HTTP stack actually sent.
        services.AddTransient<LoggingHttpMessageHandler>();

        return services;
    }
}
