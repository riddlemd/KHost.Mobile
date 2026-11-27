using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

// Auto-expire is measured from the newest AddedAt on disk against the injected clock — no timers — so every case
// here is a clock jump. It runs when asked (the Tonight tab asks as it opens) and before an add, never on a plain
// read. The rules under test: off by default, each add restarts the window, nothing else does, and an add onto an
// expired set starts a fresh one rather than reviving the old one.
public sealed class TonightStoreExpiryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 3, 4, 20, 0, 0, TimeSpan.FromHours(-6));

    private readonly TempAppDataDirectory _dir = new();
    private readonly FakeTimeProvider _clock = new(Start);
    private readonly StubSettings _settings = new() { TonightExpiryHours = 4 };

    public void Dispose() => _dir.Dispose();

    private JsonFileTonightStore NewStore(StubSettings? settings) =>
        new(_dir, timeProvider: _clock, settings: settings);

    [Fact]
    public async Task Off_keeps_the_set_however_old_it_is()
    {
        _settings.TonightExpiryHours = 0;
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromDays(30));

        Assert.False(await store.ExpireIfStaleAsync());
        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task A_store_without_settings_never_expires()
    {
        var store = NewStore(null);
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromDays(30));

        Assert.False(await store.ExpireIfStaleAsync());
        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task Clears_once_the_window_has_passed_since_the_last_add_and_persists_it()
    {
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromHours(4) - TimeSpan.FromMinutes(1));
        Assert.False(await store.ExpireIfStaleAsync());
        Assert.Equal(2, (await store.GetAllAsync()).Count);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await store.ExpireIfStaleAsync());
        Assert.Empty(await store.GetAllAsync());

        // Written through, not just dropped from the cache: a fresh store over the same file agrees.
        Assert.Empty(await NewStore(new StubSettings()).GetAllAsync());
    }

    [Fact]
    public async Task Adding_a_song_restarts_the_clock()
    {
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromHours(3));
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromHours(3));   // 6 h after the first add, 3 h after the second
        Assert.False(await store.ExpireIfStaleAsync());

        _clock.Advance(TimeSpan.FromHours(1));   // 4 h after the second
        Assert.True(await store.ExpireIfStaleAsync());
    }

    [Fact]
    public async Task Checking_off_or_reordering_does_not_restart_the_clock()
    {
        var store = NewStore(_settings);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await store.AddAsync(a);
        await store.AddAsync(b);

        _clock.Advance(TimeSpan.FromHours(3));
        await store.SetCompletedAsync(a, completed: true);
        await store.ReorderAsync([b, a]);

        _clock.Advance(TimeSpan.FromHours(1));
        Assert.True(await store.ExpireIfStaleAsync());
    }

    [Fact]
    public async Task Adding_onto_an_expired_set_starts_a_fresh_one()
    {
        var store = NewStore(_settings);
        var lastNight = Guid.NewGuid();
        await store.AddAsync(lastNight);

        _clock.Advance(TimeSpan.FromHours(20));
        var tonight = Guid.NewGuid();
        await store.AddAsync(tonight);   // first touch since the deadline — no read in between

        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Equal(tonight, entry.SongId);
        Assert.Equal(0, entry.Order);
    }

    [Fact]
    public async Task Re_adding_last_nights_song_after_expiry_queues_it_again()
    {
        var store = NewStore(_settings);
        var song = Guid.NewGuid();
        await store.AddAsync(song);

        _clock.Advance(TimeSpan.FromHours(20));
        await store.AddAsync(song);

        var entry = Assert.Single(await store.GetAllAsync());
        Assert.Equal(_clock.GetLocalNow(), entry.AddedAt);
    }

    [Fact]
    public async Task ExpireIfStale_reports_and_announces_only_a_real_expiry()
    {
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());
        var changes = 0;
        store.Changed += (_, _) => changes++;

        Assert.False(await store.ExpireIfStaleAsync());
        Assert.Equal(0, changes);

        _clock.Advance(TimeSpan.FromHours(4));
        Assert.True(await store.ExpireIfStaleAsync());
        Assert.Equal(1, changes);

        Assert.False(await store.ExpireIfStaleAsync());   // already empty
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_plain_read_never_expires_the_set()
    {
        // Only the Tonight tab's load (and an add) expire it — the tab badge and launch check just read.
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromHours(5));

        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task The_setting_is_read_live_not_captured_at_construction()
    {
        _settings.TonightExpiryHours = 0;
        var store = NewStore(_settings);
        await store.AddAsync(Guid.NewGuid());
        _clock.Advance(TimeSpan.FromHours(3));
        Assert.False(await store.ExpireIfStaleAsync());

        _settings.TonightExpiryHours = 2;
        Assert.True(await store.ExpireIfStaleAsync());
    }
}
