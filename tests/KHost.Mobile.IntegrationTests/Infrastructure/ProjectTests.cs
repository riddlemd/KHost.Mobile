using KHost.Mobile.Abstractions.Clients.CoverArt;
using KHost.Mobile.Abstractions.Clients.Lyrics;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Clients.Spotify;
using KHost.Mobile.Abstractions.Clients.Updates;
using KHost.Mobile.Abstractions.Clients.YouTubeMusic;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KHost.Mobile.IntegrationTests.Infrastructure;

/// <summary>
/// The two DI entry points. A missing or mis-lifetimed registration compiles cleanly and only fails when a
/// screen first resolves it on a device, so the whole container is built here with validation on.
/// </summary>
public sealed class ProjectTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private ServiceProvider BuildProvider(Action<IHttpClientBuilder>? configureHandler = null)
    {
        var services = new ServiceCollection();

        // What the MAUI head supplies and these two methods deliberately don't.
        services.AddSingleton<IAppDataDirectory>(_dir);
        services.AddSingleton<IAppSettings>(new StubSettings());
        services.AddSingleton<ILocationProvider>(new StubLocationProvider());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddHttpClient();

        global::KHost.Mobile.Infrastructure.Project.AddKHostInfrastructure(services);
        global::KHost.Mobile.Clients.Project.AddKHostClients(services, "KHostCue/1.0 (tests)", configureHandler);

        // ValidateOnBuild walks every constructor, so a service no test names by hand still fails here.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Theory]
    [InlineData(typeof(ISongListStore))]
    [InlineData(typeof(ITonightStore))]
    [InlineData(typeof(IVenueStore))]
    [InlineData(typeof(ISingerStore))]
    [InlineData(typeof(ILyricsCache))]
    [InlineData(typeof(IAppSession))]
    [InlineData(typeof(IVenueLocator))]
    [InlineData(typeof(IAlbumArtCache))]
    [InlineData(typeof(IQrCodeService))]
    [InlineData(typeof(ISafeAreaInsets))]
    [InlineData(typeof(IBackButtonService))]
    [InlineData(typeof(ILookupOptions))]
    [InlineData(typeof(IAtomicFileWriter))]
    [InlineData(typeof(ISongLinkBuilder))]
    [InlineData(typeof(IKaraFunVenueUrlParser))]
    [InlineData(typeof(ITimeFormatter))]
    [InlineData(typeof(IAppVersionParser))]
    [InlineData(typeof(IDateTimeInputConverter))]
    [InlineData(typeof(IRatingScorer))]
    [InlineData(typeof(ISurprisePicker))]
    [InlineData(typeof(ISingerProfileCodec))]
    [InlineData(typeof(ISingerFileNames))]
    [InlineData(typeof(ISongEnricher))]
    [InlineData(typeof(TimeProvider))]
    [InlineData(typeof(Random))]
    public void Infrastructure_resolves_every_service_it_registers(Type service)
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(service));
    }

    [Theory]
    [InlineData(typeof(ITrackMetadataLookup))]
    [InlineData(typeof(ICoverArtLookup))]
    [InlineData(typeof(ISpellingSuggestionLookup))]
    [InlineData(typeof(ILyricsLookup))]
    [InlineData(typeof(IUpdateLookup))]
    [InlineData(typeof(ISpotifyImportService))]
    [InlineData(typeof(IYouTubeMusicImportService))]
    public void Clients_resolves_every_backend_it_registers(Type service)
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(service));
    }

    [Fact]
    public void The_song_list_store_is_one_instance_behind_both_of_its_registrations()
    {
        // Export/import resolves the concrete store, every screen the interface. Collapsing this to a plain
        // AddSingleton<ISongListStore, JsonFileSongListStore>() splits the cache, so an import lands unread.
        using var provider = BuildProvider();

        Assert.Same(
            provider.GetRequiredService<JsonFileSongListStore>(),
            provider.GetRequiredService<ISongListStore>());
    }

    [Fact]
    public void The_stores_are_singletons_so_every_screen_shares_one_cache()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<ITonightStore>(), provider.GetRequiredService<ITonightStore>());
        Assert.Same(provider.GetRequiredService<IVenueStore>(), provider.GetRequiredService<IVenueStore>());
        Assert.Same(provider.GetRequiredService<ISingerStore>(), provider.GetRequiredService<ISingerStore>());
        Assert.Same(provider.GetRequiredService<IAppSession>(), provider.GetRequiredService<IAppSession>());
    }

    [Fact]
    public void Every_typed_client_is_offered_to_the_hosts_handler_hook()
    {
        // The hook is how LoggingHttpMessageHandler reaches clients that do no logging of their own — a backend
        // registered around it goes silent on-device.
        var configured = 0;

        using var provider = BuildProvider(_ => configured++);

        Assert.Equal(7, configured);
    }

    [Fact]
    public void The_lyrics_and_update_clients_carry_the_base_address_and_headers_their_apis_demand()
    {
        // GitHub rejects a request with no User-Agent outright, and both clients call relative paths.
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var lyrics = factory.CreateClient(nameof(ILyricsLookup));
        Assert.Equal(new Uri("https://lrclib.net/"), lyrics.BaseAddress);
        Assert.Contains("KHostCue", lyrics.DefaultRequestHeaders.UserAgent.ToString());

        var updates = factory.CreateClient(nameof(IUpdateLookup));
        Assert.Equal(new Uri("https://api.github.com/"), updates.BaseAddress);
        Assert.Contains("KHostCue", updates.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Contains("application/vnd.github+json", updates.DefaultRequestHeaders.Accept.ToString());
    }

    [Fact]
    public void A_blank_user_agent_is_refused_rather_than_sent()
    {
        // An empty UA is what GitHub 403s on — better to fail at startup than to lose update checks silently.
        Assert.Throws<ArgumentException>(
            () => global::KHost.Mobile.Clients.Project.AddKHostClients(new ServiceCollection(), "   "));
    }

    private sealed class StubLocationProvider : ILocationProvider
    {
        public Task<GeoPoint?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<GeoPoint?>(null);
    }

    // IAppSettings is 39 tunables wide; nothing here reads a value, only the registration graph is under test.
    private sealed class StubSettings : IAppSettings
    {
        public bool AutoFillMetadata { get; set; }
        public bool TonightEnabled { get; set; }
        public string LastActiveSingerId { get; set; } = "";
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
}
