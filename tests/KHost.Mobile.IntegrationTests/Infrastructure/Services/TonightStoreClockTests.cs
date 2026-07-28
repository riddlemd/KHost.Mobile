using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

// The store stamps AddedAt/CompletedAt from an injected TimeProvider rather than reading the clock, so these
// assert on exact instants instead of tolerating wall-clock drift. The local-vs-UTC check is the one that
// matters: every timestamp already on a device is local, so a switch to GetUtcNow would silently shift new
// ones by the UTC offset and corrupt "sung today" narrowing and recency decay against existing data.
public sealed class TonightStoreClockTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Stamps_AddedAt_and_CompletedAt_from_the_injected_clock()
    {
        var added = new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.FromHours(-5));
        var clock = new FakeTimeProvider(added);
        var store = new JsonFileTonightStore(_dir, timeProvider: clock);
        var song = Guid.NewGuid();

        await store.AddAsync(song);

        var completed = added.AddHours(2);
        clock.SetUtcNow(completed);
        await store.SetCompletedAsync(song, completed: true);

        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Equal(added, entry.AddedAt);
        Assert.Equal(completed, entry.CompletedAt);
    }

    [Fact]
    public async Task Unchecking_clears_CompletedAt_without_touching_AddedAt()
    {
        var start = new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.FromHours(-5));
        var clock = new FakeTimeProvider(start);
        var store = new JsonFileTonightStore(_dir, timeProvider: clock);
        var song = Guid.NewGuid();

        await store.AddAsync(song);
        clock.Advance(TimeSpan.FromMinutes(30));
        await store.SetCompletedAsync(song, completed: true);
        clock.Advance(TimeSpan.FromMinutes(5));
        await store.SetCompletedAsync(song, completed: false);

        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Null(entry.CompletedAt);
        Assert.Equal(start, entry.AddedAt);
    }

    [Fact]
    public async Task Stamps_local_time_not_UTC()
    {
        // A zone whose offset is non-zero at the instant under test, so local and UTC are distinguishable.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(-5), "t", "t"));
        var store = new JsonFileTonightStore(_dir, timeProvider: clock);

        await store.AddAsync(Guid.NewGuid());

        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Equal(TimeSpan.FromHours(-5), entry.AddedAt.Offset);
        Assert.Equal(clock.GetUtcNow(), entry.AddedAt.ToUniversalTime());
    }
}
