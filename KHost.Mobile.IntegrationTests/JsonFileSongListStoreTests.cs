using System.Text.Json;
using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

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
        Assert.Equal("Edited", (await store.GetAllAsync())[0].Title);

        await store.UpdateAsync(new SongListItem { Title = "Ghost" });   // never added → no-op
        Assert.Single(await store.GetAllAsync());
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
    public async Task ImportAsync_migrates_a_legacy_entry_into_performances()
    {
        var store = NewStore();
        var sungAt = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);

        await store.ImportAsync([new SongListItem { Title = "Legacy", Artist = "A", SungDates = [sungAt], Confidence = 4 }]);

        var song = Assert.Single(await store.GetAllAsync());
        var performance = Assert.Single(song.Performances);
        Assert.Equal(4, performance.HowItWent);
        Assert.Equal(sungAt, performance.Date);
        Assert.Empty(song.SungDates);         // legacy fields emptied after migration
        Assert.Equal(0, song.Confidence);
        Assert.Equal(SongListItemStatus.Sang, song.Status);
    }

    [Fact]
    public async Task GetAllAsync_migrates_a_legacy_file_on_load()
    {
        var sungAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var legacy = new SongListItem { Title = "Legacy", Artist = "A", SungDates = [sungAt], Confidence = 3 };
        await File.WriteAllTextAsync(
            _dir.FilePath("song-list.json"),
            JsonSerializer.Serialize(new List<SongListItem> { legacy }, SongListJsonContext.Default.ListSongListItem));

        var song = Assert.Single(await NewStore().GetAllAsync());

        var performance = Assert.Single(song.Performances);
        Assert.Equal(3, performance.HowItWent);
        Assert.Equal(sungAt, performance.Date);
        Assert.Empty(song.SungDates);
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
}
