using System.Text.Json;
using KHost.Mobile.Infrastructure.Serialization;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

public sealed class JsonFileSongListStoreTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    private JsonFileSongListStore NewStore() => new(_dir);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task AddAsync_trims_input_blanks_to_null_and_starts_on_the_wishlist()
    {
        var store = NewStore();

        var item = await store.AddAsync("  Bohemian Rhapsody  ", "  Queen  ", notes: "   ", genre: "  Rock ", year: 1975);

        Assert.Equal("Bohemian Rhapsody", item.Title);
        Assert.Equal("Queen", item.Artist);
        Assert.Null(item.Notes);            // whitespace-only → null
        Assert.Equal("Rock", item.Genre);
        Assert.Equal(1975, item.Year);
        Assert.Equal(SongListItemStatus.WantToSing, item.Status);
        Assert.Empty(item.Performances);
    }

    [Fact]
    public async Task GetAllAsync_returns_newest_first()
    {
        var store = NewStore();
        var older = new SongListItem { Title = "Older", Artist = "A", AddedAt = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var newer = new SongListItem { Title = "Newer", Artist = "B", AddedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        await store.ImportAsync([older, newer]);

        var all = await store.GetAllAsync();

        Assert.Equal(["Newer", "Older"], all.Select(i => i.Title));
    }

    [Fact]
    public async Task State_persists_to_disk_and_is_read_back_by_a_fresh_instance()
    {
        var writer = NewStore();
        await writer.AddAsync("Africa", "Toto");

        // A brand-new instance shares only the folder, not the in-memory cache — so this proves the JSON round-trip.
        var reader = NewStore();
        var all = await reader.GetAllAsync();

        var song = Assert.Single(all);
        Assert.Equal("Africa", song.Title);
        Assert.Equal("Toto", song.Artist);
    }

    [Fact]
    public async Task Changed_fires_on_real_mutations_but_not_on_no_ops()
    {
        var store = NewStore();
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.AddAsync("A", "B");                 // real change → fires
        Assert.Equal(1, fired);

        await store.RemoveAsync(Guid.NewGuid());        // id not present → no-op
        Assert.Equal(1, fired);

        await store.ClearAsync();                        // list has one → fires
        Assert.Equal(2, fired);

        await store.ClearAsync();                        // already empty → no-op
        Assert.Equal(2, fired);
    }

    [Fact]
    public async Task UpdateAsync_persists_a_matching_item_and_ignores_an_unknown_id()
    {
        var store = NewStore();
        var item = await store.AddAsync("Original", "A");

        item.Title = "Edited";
        await store.UpdateAsync(item);
        Assert.Equal("Edited", (await NewStore().GetAllAsync())[0].Title);   // a fresh instance, so this is disk

        await store.UpdateAsync(new SongListItem { Title = "Ghost" });   // never added → no-op
        Assert.Single(await NewStore().GetAllAsync());
    }

    [Fact]
    public async Task RestoreAsync_reinserts_once_and_is_idempotent()
    {
        var store = NewStore();
        var item = await store.AddAsync("A", "B");
        await store.RemoveAsync(item.Id);
        Assert.Empty(await store.GetAllAsync());

        await store.RestoreAsync(item);
        Assert.Single(await store.GetAllAsync());

        await store.RestoreAsync(item);   // double undo → no duplicate
        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task ImportAsync_skips_duplicates_within_the_batch_and_against_the_existing_list()
    {
        var store = NewStore();
        await store.AddAsync("Existing", "Artist");

        var added = await store.ImportAsync(
        [
            new SongListItem { Title = "existing", Artist = "ARTIST" },   // case-insensitive dup of existing → skip
            new SongListItem { Title = "New One", Artist = "X" },
            new SongListItem { Title = "New One", Artist = "X" },         // dup earlier in this batch → skip
            new SongListItem { Title = "   ", Artist = "Blank" },         // blank title → ignored
        ]);

        Assert.Equal(1, added);
        Assert.Equal(2, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ImportAsync_can_keep_duplicates_when_asked()
    {
        var store = NewStore();

        var added = await store.ImportAsync(
        [
            new SongListItem { Title = "Dup", Artist = "A" },
            new SongListItem { Title = "Dup", Artist = "A" },
        ], skipDuplicates: false);

        Assert.Equal(2, added);
    }



    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_list_rather_than_throwing()
    {
        await File.WriteAllTextAsync(_dir.FilePath("song-list.json"), "this is not json{");

        Assert.Empty(await NewStore().GetAllAsync());
    }

    [Fact]
    public async Task Tags_round_trip_through_disk()
    {
        var writer = NewStore();
        var item = await writer.AddAsync("Mr. Brightside", "The Killers");
        item.Tags = ["closer", "high energy", "crowd pleaser"];
        await writer.UpdateAsync(item);

        // A fresh instance shares only the folder, not the in-memory cache — proves the JSON round-trip.
        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal(["closer", "high energy", "crowd pleaser"], song.Tags);
    }

    [Fact]
    public async Task A_song_saved_before_tags_existed_deserializes_to_an_empty_list()
    {
        // A file whose song object has no "Tags" property at all (pre-feature shape).
        await File.WriteAllTextAsync(
            _dir.FilePath("song-list.json"),
            """[ { "Id": "11111111-1111-1111-1111-111111111111", "Title": "Old", "Artist": "A" } ]""");

        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.NotNull(song.Tags);
        Assert.Empty(song.Tags);
    }

    [Fact]
    public async Task SuggestedTitle_and_SuggestedArtist_survive_save_and_reload()
    {
        var writer = NewStore();
        var item = await writer.AddAsync("Bohemian Rapsody", "Queen");
        item.SuggestedTitle = "Bohemian Rhapsody";
        item.SuggestedArtist = "Queen";
        await writer.UpdateAsync(item);

        // A fresh instance shares only the folder, not the in-memory cache — proves the JSON round-trip.
        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("Bohemian Rhapsody", song.SuggestedTitle);
        Assert.Equal("Queen", song.SuggestedArtist);
        Assert.True(song.HasSuggestion);
    }

    [Fact]
    public async Task ArtworkUrl_ArtworkLookedUp_and_MetadataLookedUp_survive_save_and_reload()
    {
        var writer = NewStore();
        var item = await writer.AddAsync("Africa", "Toto");
        item.ArtworkUrl = "https://example.com/cover.jpg";
        item.ArtworkLookedUp = true;
        item.MetadataLookedUp = true;
        await writer.UpdateAsync(item);

        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("https://example.com/cover.jpg", song.ArtworkUrl);
        Assert.True(song.ArtworkLookedUp);
        Assert.True(song.MetadataLookedUp);
    }

    [Fact]
    public async Task IsFavorite_and_Enjoyment_survive_save_and_reload()
    {
        var writer = NewStore();
        var item = await writer.AddAsync("Africa", "Toto");
        item.IsFavorite = true;
        item.Enjoyment = 4;
        await writer.UpdateAsync(item);

        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.True(song.IsFavorite);
        Assert.Equal(4, song.Enjoyment);
    }

    [Fact]
    public async Task Reads_the_PascalCase_property_names_already_on_devices()
    {
        // A naming policy added to SongListJsonContext would round-trip through itself in every other test
        // while orphaning every song-list.json already on a device.
        await File.WriteAllTextAsync(
            _dir.FilePath("song-list.json"),
            """
            [ { "Id": "d1000001-0000-4000-8000-000000000001", "Title": "Africa", "Artist": "Toto",
                "Genre": "Rock", "Year": 1982, "Status": 1, "IsFavorite": true, "Enjoyment": 4,
                "Tags": [ "closer" ],
                "Performances": [ { "Id": "e1000001-0000-4000-8000-000000000002",
                                    "Date": "2026-03-04T20:15:00-05:00", "HowItWent": 5, "Note": "nailed it" } ] } ]
            """);

        var song = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal("Africa", song.Title);
        Assert.Equal("Toto", song.Artist);
        Assert.Equal(1982, song.Year);
        Assert.True(song.IsFavorite);
        Assert.Equal(4, song.Enjoyment);
        Assert.Equal(["closer"], song.Tags);
        var performance = Assert.Single(song.Performances);
        Assert.Equal(5, performance.HowItWent);
        Assert.Equal("nailed it", performance.Note);
    }

    [Fact]
    public async Task UpdateRangeAsync_replaces_known_ids_skips_unknown_ones_and_fires_once()
    {
        // Appending an unknown id would duplicate songs; firing unconditionally would re-render the list on
        // every artwork-lookup poll.
        var store = NewStore();
        var africa = await store.AddAsync("Africa", "Toto");
        await store.AddAsync("Rosanna", "Toto");
        var fired = 0;
        store.Changed += (_, _) => fired++;

        africa.Genre = "Rock";
        await store.UpdateRangeAsync([africa, new SongListItem { Title = "Ghost", Artist = "Nobody" }]);

        var all = await NewStore().GetAllAsync();
        Assert.Equal(2, all.Count);                                    // the unknown id was skipped, not appended
        Assert.Equal("Rock", all.Single(s => s.Title == "Africa").Genre);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task UpdateRangeAsync_with_nothing_it_knows_neither_saves_nor_fires()
    {
        var store = NewStore();
        await store.AddAsync("Africa", "Toto");
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.UpdateRangeAsync([new SongListItem { Title = "Ghost", Artist = "Nobody" }]);

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task Import_dedupe_does_not_collide_on_a_different_title_artist_split()
    {
        // The dedupe key joins title and artist with a separator for exactly this reason — plain concatenation
        // would make "AB"/"C" and "A"/"BC" the same song and silently drop the second on import.
        var store = NewStore();

        var added = await store.ImportAsync([
            new SongListItem { Title = "AB", Artist = "C" },
            new SongListItem { Title = "A", Artist = "BC" },
        ]);

        Assert.Equal(2, added);
        Assert.Equal(2, (await NewStore().GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetAllAsync_hands_back_a_copy_so_a_caller_cannot_edit_the_cache()
    {
        // The UI sorts and filters what it gets back; if that were the cached list, the next save would persist
        // whatever the component did to it.
        var store = NewStore();
        await store.AddAsync("Africa", "Toto");

        (await store.GetAllAsync() as List<SongListItem>)!.Clear();

        Assert.Single(await store.GetAllAsync());
    }
}
