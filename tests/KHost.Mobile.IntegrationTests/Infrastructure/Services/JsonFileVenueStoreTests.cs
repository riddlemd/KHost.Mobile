using System.Text.Json;
using KHost.Mobile.Infrastructure.Serialization;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

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
        var got = await NewStore().GetAsync(added.Id);   // a fresh instance, so this is disk not the cache
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
    public async Task A_corrupt_file_loads_as_an_empty_list_and_is_quarantined_to_a_dot_corrupt_sibling()
    {
        var path = _dir.FilePath("venues.json");
        await File.WriteAllTextAsync(path, "}not valid{");   // e.g. a pre-atomic-write interrupted save

        Assert.Empty(await NewStore().GetAllAsync());

        Assert.False(File.Exists(path));                    // the bad file was moved aside...
        Assert.True(File.Exists(path + ".corrupt"));         // ...to a .corrupt sibling...
        Assert.Equal("}not valid{", await File.ReadAllTextAsync(path + ".corrupt"));   // ...with its bytes intact
    }

    [Fact]
    public async Task Reads_the_PascalCase_property_names_already_on_devices()
    {
        // Literal JSON, not a re-serialized list: a naming policy added to VenueJsonContext would still round-trip
        // through itself while orphaning every venues.json already written to a device.
        await File.WriteAllTextAsync(
            _dir.FilePath("venues.json"),
            """
            [ { "Id": "a1000001-0000-4000-8000-000000000001", "Name": "The Dive", "Glyph": "🍸",
                "IsFavorite": true, "KaraFunVenueId": "999", "Latitude": 34.09, "Longitude": -118.34 } ]
            """);

        var got = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("The Dive", got.Name);
        Assert.Equal("🍸", got.Glyph);
        Assert.True(got.IsFavorite);
        Assert.Equal("999", got.KaraFunVenueId);
        Assert.True(got.HasLocation);
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
    public async Task A_file_without_ShowInSwitcher_defaults_it_to_true()
    {
        // A venue file written before the field existed must keep the venue listed, not silently hide it.
        await File.WriteAllTextAsync(
            _dir.FilePath("venues.json"),
            """[ { "Id": "a1000001-0000-4000-8000-000000000001", "Name": "Old Venue", "Glyph": "🎤" } ]""");

        var got = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("Old Venue", got.Name);   // the rest of the venue bound, so the default is what's under test
        Assert.True(got.ShowInSwitcher);
    }

    [Fact]
    public async Task AddAsync_keeps_an_id_the_caller_supplied()
    {
        // The venue-import merge relies on this: ids are kept so performances tagged with a venue keep resolving.
        var id = Guid.NewGuid();

        var added = await NewStore().AddAsync(new Venue { Id = id, Name = "The Mint" });

        Assert.Equal(id, added.Id);
        Assert.Equal(id, (await NewStore().GetAllAsync()).Single().Id);
    }

    [Fact]
    public async Task AddAsync_re_keys_a_venue_whose_id_is_already_taken()
    {
        // Adding is never an overwrite. Two venues sharing an id are indistinguishable to UpdateAsync (it edits
        // the first) and to RemoveAsync (it deletes both), so an id arriving from outside can't be trusted.
        var id = Guid.NewGuid();
        var store = NewStore();
        await store.AddAsync(new Venue { Id = id, Name = "The Mint" });

        var second = await store.AddAsync(new Venue { Id = id, Name = "Lucky Strike" });

        Assert.NotEqual(id, second.Id);
        var all = await NewStore().GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(2, all.Select(v => v.Id).Distinct().Count());
    }

    [Fact]
    public async Task Removing_a_re_keyed_venue_leaves_the_one_it_collided_with()
    {
        // The payoff for re-keying: without it, RemoveAsync's RemoveAll would take both venues at once.
        var id = Guid.NewGuid();
        var store = NewStore();
        await store.AddAsync(new Venue { Id = id, Name = "The Mint" });
        var second = await store.AddAsync(new Venue { Id = id, Name = "Lucky Strike" });

        await store.RemoveAsync(second.Id);

        var survivor = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("The Mint", survivor.Name);
        Assert.Equal(id, survivor.Id);
    }

    [Fact]
    public async Task GetAllAsync_hands_back_a_copy_so_a_caller_cannot_edit_the_cache()
    {
        var store = NewStore();
        await store.AddAsync(new Venue { Name = "The Mint" });

        (await store.GetAllAsync() as List<Venue>)!.Clear();

        Assert.Single(await store.GetAllAsync());
    }
}
