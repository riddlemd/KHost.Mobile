using System.Text.Json;
using KHost.Mobile.Abstractions.Clients.Lyrics;
using KHost.Mobile.Infrastructure.Serialization;
using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

// The stores take TimeProvider as an OPTIONAL parameter (so the tests can `new` them bare), which means DI
// silently falling back to the TimeProvider.System default would be invisible — the app would behave
// identically and only a frozen-clock test would ever notice. These assert the container really does supply
// the registered clock, mirroring how MauiProgram registers it.
public sealed class StoreClockInjectionTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private ServiceProvider BuildProvider(TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppDataDirectory>(_dir);
        services.AddSingleton(clock);
        services.AddSingleton<ITonightStore, JsonFileTonightStore>();
        services.AddSingleton<ILyricsCache, JsonFileLyricsCache>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Container_supplies_the_registered_clock_to_the_tonight_store()
    {
        var frozen = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
        using var provider = BuildProvider(new FakeTimeProvider(frozen));

        var store = provider.GetRequiredService<ITonightStore>();
        await store.AddAsync(Guid.NewGuid());

        // If the optional parameter were NOT injected the store would have used TimeProvider.System, and this
        // would be "now" rather than the frozen instant.
        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Equal(frozen, entry.AddedAt);
    }

    [Fact]
    public async Task Container_supplies_the_registered_clock_to_the_lyrics_cache()
    {
        var frozen = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
        using var provider = BuildProvider(new FakeTimeProvider(frozen));

        var cache = provider.GetRequiredService<ILyricsCache>();
        await cache.SetAsync("Africa", "Toto", new LyricsResult("Africa", "Toto", "some lyrics", null, false));

        // CachedAt isn't exposed through LyricsCacheHit, so read it back off disk.
        var stored = JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(_dir.FilePath("lyrics-cache.json")),
            LyricsCacheJsonContext.Default.ListLyricsCacheEntry);

        Assert.Equal(frozen, Assert.Single(stored!).CachedAt);
    }
}
