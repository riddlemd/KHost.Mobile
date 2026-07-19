using System.Text.Json;
using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

public sealed class JsonFileSingerStoreTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    private JsonFileSingerStore NewStore() => new(_dir);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task AddAsync_assigns_an_id_and_appends_order()
    {
        var store = NewStore();

        var a = await store.AddAsync(new Singer { Name = "Mike" });
        var b = await store.AddAsync(new Singer { Name = "Sam" });

        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.Equal(0, a.Order);
        Assert.Equal(1, b.Order);
    }

    [Fact]
    public async Task GetAllAsync_orders_by_order()
    {
        var store = NewStore();
        var first = await store.AddAsync(new Singer { Name = "First" });
        var second = await store.AddAsync(new Singer { Name = "Second" });
        // Re-order by editing Order, then confirm read order follows it (not add order).
        second.Order = -1;
        await store.UpdateAsync(second);

        var names = (await store.GetAllAsync()).Select(s => s.Name).ToArray();

        Assert.Equal(["Second", "First"], names);
        Assert.Equal(first.Id, (await store.GetAsync(first.Id))!.Id);
    }

    [Fact]
    public async Task EnsureSeededAsync_creates_one_default_singer_and_is_idempotent()
    {
        var store = NewStore();

        var seeded = await store.EnsureSeededAsync();
        Assert.False(string.IsNullOrWhiteSpace(seeded.Name));
        Assert.Single(await store.GetAllAsync());

        // Second call is a no-op — still exactly one, same id.
        var again = await store.EnsureSeededAsync();
        Assert.Equal(seeded.Id, again.Id);
        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task EnsureSeededAsync_migrates_the_legacy_single_user_song_list_into_the_seeded_singer()
    {
        // A pre-multi-singer install: a single song-list.json at the root with the user's songs.
        var legacy = new List<SongListItem> { new() { Title = "Africa", Artist = "Toto" } };
        await File.WriteAllTextAsync(
            _dir.FilePath("song-list.json"),
            JsonSerializer.Serialize(legacy, SongListJsonContext.Default.ListSongListItem));

        var store = NewStore();
        var me = await store.EnsureSeededAsync();

        // The legacy file is moved into the seeded singer's namespaced file.
        Assert.False(File.Exists(_dir.FilePath("song-list.json")));
        Assert.True(File.Exists(_dir.FilePath($"song-list-{me.Id:N}.json")));

        // And that file still holds the user's songs, now owned by the seeded singer.
        var session = new AppSession();
        session.SetActiveSinger(me.Id);
        var songs = await new JsonFileSongListStore(_dir, session).GetAllAsync();
        Assert.Equal("Africa", Assert.Single(songs).Title);
    }

    [Fact]
    public async Task RemoveAsync_deletes_the_singer_and_their_data_files()
    {
        var store = NewStore();
        var s = await store.AddAsync(new Singer { Name = "Guest" });
        // Give the singer a song file to prove it's cleaned up on removal.
        var session = new AppSession();
        session.SetActiveSinger(s.Id);
        await new JsonFileSongListStore(_dir, session).AddAsync("One Song", "An Artist");
        Assert.True(File.Exists(_dir.FilePath($"song-list-{s.Id:N}.json")));

        await store.RemoveAsync(s.Id);

        Assert.Empty(await store.GetAllAsync());
        Assert.False(File.Exists(_dir.FilePath($"song-list-{s.Id:N}.json")));
    }

    [Fact]
    public async Task State_persists_to_disk_and_is_read_back_by_a_fresh_instance()
    {
        var writer = NewStore();
        var s = await writer.AddAsync(new Singer { Name = "Jordan", Color = "#d97706" });

        var got = await NewStore().GetAsync(s.Id);
        Assert.NotNull(got);
        Assert.Equal("Jordan", got!.Name);
        Assert.Equal("#d97706", got.Color);
    }

    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_roster_rather_than_throwing()
    {
        await File.WriteAllTextAsync(_dir.FilePath("singers.json"), "}not valid{");

        Assert.Empty(await NewStore().GetAllAsync());
    }

    [Fact]
    public async Task Glyph_round_trips_and_drives_the_avatar()
    {
        var store = NewStore();
        var withEmoji = await store.AddAsync(new Singer { Name = "Sam", Glyph = "🦄" });
        var withLetter = await store.AddAsync(new Singer { Name = "Jordan" });   // no glyph → letter avatar

        var freshEmoji = await NewStore().GetAsync(withEmoji.Id);
        Assert.Equal("🦄", freshEmoji!.Glyph);
        Assert.Equal("🦄", freshEmoji.Avatar);          // glyph wins

        var freshLetter = await NewStore().GetAsync(withLetter.Id);
        Assert.Null(freshLetter!.Glyph);
        Assert.Equal("J", freshLetter.Avatar);          // falls back to the name's first letter
    }
}
