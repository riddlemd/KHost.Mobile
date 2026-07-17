using System.Text.Json;
using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

public sealed class JsonFileVenueStoreTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    private JsonFileVenueStore NewStore() => new(_dir);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task AddAsync_assigns_an_id_when_blank_and_persists()
    {
        var store = NewStore();

        var added = await store.AddAsync(new Venue { Name = "The Mint" });

        Assert.NotEqual(Guid.Empty, added.Id);
        var got = await store.GetAsync(added.Id);
        Assert.NotNull(got);
        Assert.Equal("The Mint", got!.Name);
    }

    [Fact]
    public async Task GetAllAsync_sorts_favorites_first_then_by_name()
    {
        var store = NewStore();
        await store.AddAsync(new Venue { Name = "Zebra Bar" });
        await store.AddAsync(new Venue { Name = "Palms", IsFavorite = true });
        await store.AddAsync(new Venue { Name = "Anchor" });

        var names = (await store.GetAllAsync()).Select(v => v.Name).ToArray();

        Assert.Equal(["Palms", "Anchor", "Zebra Bar"], names);
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_matching_venue()
    {
        var store = NewStore();
        var v = await store.AddAsync(new Venue { Name = "The Mint", KaraFunVenueId = null });

        v.Name = "The Mint (Thu)";
        v.KaraFunVenueId = "012345";
        await store.UpdateAsync(v);

        var got = await store.GetAsync(v.Id);
        Assert.Equal("The Mint (Thu)", got!.Name);
        Assert.Equal("012345", got.KaraFunVenueId);
    }

    [Fact]
    public async Task UpdateAsync_is_a_no_op_for_an_unknown_id()
    {
        var store = NewStore();
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.UpdateAsync(new Venue { Id = Guid.NewGuid(), Name = "ghost" });

        Assert.Empty(await store.GetAllAsync());
        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task RemoveAsync_deletes_by_id_and_no_ops_when_absent()
    {
        var store = NewStore();
        var v = await store.AddAsync(new Venue { Name = "The Mint" });
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.RemoveAsync(v.Id);
        Assert.Empty(await store.GetAllAsync());
        Assert.Equal(1, fired);

        await store.RemoveAsync(v.Id);   // already gone → no-op
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task State_persists_to_disk_and_is_read_back_by_a_fresh_instance()
    {
        var writer = NewStore();
        var v = await writer.AddAsync(new Venue { Name = "Palms", Glyph = "🌴", KaraFunVenueId = "999" });

        var reader = NewStore();
        var got = await reader.GetAsync(v.Id);
        Assert.NotNull(got);
        Assert.Equal("Palms", got!.Name);
        Assert.Equal("🌴", got.Glyph);
        Assert.Equal("999", got.KaraFunVenueId);
    }

    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_list_rather_than_throwing()
    {
        await File.WriteAllTextAsync(_dir.FilePath("venues.json"), "}not valid{");

        Assert.Empty(await NewStore().GetAllAsync());
    }

    [Fact]
    public async Task Deserializes_a_hand_written_file()
    {
        var seeded = new List<Venue> { new() { Name = "The Dive", Glyph = "🍸" } };
        await File.WriteAllTextAsync(
            _dir.FilePath("venues.json"),
            JsonSerializer.Serialize(seeded, VenueJsonContext.Default.ListVenue));

        var got = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("The Dive", got.Name);
        Assert.Equal("🍸", got.Glyph);
    }

    [Fact]
    public async Task ShowInSwitcher_false_round_trips_to_disk()
    {
        var writer = NewStore();
        var v = await writer.AddAsync(new Venue { Name = "Backup Room", ShowInSwitcher = false });

        var got = await NewStore().GetAsync(v.Id);
        Assert.NotNull(got);
        Assert.False(got!.ShowInSwitcher);
    }

    [Fact]
    public async Task UpdateAsync_persists_a_toggled_ShowInSwitcher()
    {
        var store = NewStore();
        var v = await store.AddAsync(new Venue { Name = "The Mint" });

        v.ShowInSwitcher = false;
        await store.UpdateAsync(v);

        var got = await NewStore().GetAsync(v.Id);
        Assert.False(got!.ShowInSwitcher);
    }

    [Fact]
    public async Task A_legacy_file_without_ShowInSwitcher_defaults_it_to_true()
    {
        // A venue file written before the field existed must keep the venue listed, not silently hide it.
        await File.WriteAllTextAsync(
            _dir.FilePath("venues.json"),
            """[ { "Id": "a1000001-0000-4000-8000-000000000001", "Name": "Old Venue", "Glyph": "🎤" } ]""");

        var got = Assert.Single(await NewStore().GetAllAsync());
        Assert.True(got.ShowInSwitcher);
    }
}
