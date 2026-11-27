using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

// Each Tonight entry remembers the venue that was active when it was added, so the page can group the set by
// where each song was signed up. The stamp is the store's job, not the caller's — every add path gets it.
public sealed class TonightStoreVenueTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();
    private readonly AppSession _session = new();

    public TonightStoreVenueTests() => _session.SetActiveSinger(Guid.NewGuid());

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Stamps_the_active_venue_at_the_moment_of_adding()
    {
        var store = new JsonFileTonightStore(_dir, _session);
        var mint = Guid.NewGuid();
        var palms = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _session.SetActiveVenue(palms);
        await store.AddAsync(first);
        _session.SetActiveVenue(mint);   // moving on doesn't rewrite where the first song was signed up
        await store.AddAsync(second);

        var set = await store.GetAllAsync();
        Assert.Equal(palms, set.Single(e => e.SongId == first).VenueId);
        Assert.Equal(mint, set.Single(e => e.SongId == second).VenueId);
    }

    [Fact]
    public async Task No_active_venue_leaves_it_null()
    {
        var store = new JsonFileTonightStore(_dir, _session);
        await store.AddAsync(Guid.NewGuid());

        Assert.Null(Assert.Single(await store.GetAllAsync()).VenueId);
    }

    [Fact]
    public async Task Re_adding_a_queued_song_keeps_its_original_venue()
    {
        var store = new JsonFileTonightStore(_dir, _session);
        var song = Guid.NewGuid();
        var palms = Guid.NewGuid();
        _session.SetActiveVenue(palms);
        await store.AddAsync(song);

        _session.SetActiveVenue(Guid.NewGuid());
        await store.AddAsync(song);

        Assert.Equal(palms, Assert.Single(await store.GetAllAsync()).VenueId);
    }

    [Fact]
    public async Task Survives_a_reload_from_disk()
    {
        var venue = Guid.NewGuid();
        _session.SetActiveVenue(venue);
        await new JsonFileTonightStore(_dir, _session).AddAsync(Guid.NewGuid());

        var reloaded = new JsonFileTonightStore(_dir, _session);
        Assert.Equal(venue, Assert.Single(await reloaded.GetAllAsync()).VenueId);
    }

    [Fact]
    public async Task A_set_saved_before_the_field_existed_loads_with_no_venue()
    {
        var store = new JsonFileTonightStore(_dir);   // no session → the unsuffixed file
        await File.WriteAllTextAsync(_dir.FilePath("tonight.json"),
            $$"""[{ "SongId": "{{Guid.NewGuid()}}", "Order": 0, "AddedAt": "2026-03-04T21:00:00-06:00" }]""");

        Assert.Null(Assert.Single(await store.GetAllAsync()).VenueId);
    }
}
