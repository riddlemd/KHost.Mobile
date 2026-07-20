using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

// The store-level behavior behind the Import/Export page's "import a profile" flow: the id-preserving upsert
// (MergeByIdAsync) and restoring a profile into a freshly-created singer's own namespaced file.
public sealed class SingerProfileImportTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task MergeByIdAsync_appends_new_ids_and_replaces_existing_verbatim()
    {
        var store = new JsonFileSongListStore(_dir);
        var a = new SongListItem { Id = Guid.NewGuid(), Title = "A", Artist = "x" };
        await store.MergeByIdAsync([a]);

        // Restore: replace a by its id (new title + a logged performance), and add a brand-new song b.
        var aRestored = new SongListItem
        {
            Id = a.Id,
            Title = "A (restored)",
            Artist = "x",
            Performances = [new Performance { HowItWent = 5 }],
        };
        var b = new SongListItem { Id = Guid.NewGuid(), Title = "B", Artist = "y" };

        var written = await store.MergeByIdAsync([aRestored, b]);

        Assert.Equal(2, written);
        var all = await new JsonFileSongListStore(_dir).GetAllAsync();
        Assert.Equal(2, all.Count);
        var reA = all.Single(s => s.Id == a.Id);
        Assert.Equal("A (restored)", reA.Title);       // replaced in place, not duplicated
        Assert.Single(reA.Performances);               // history came across
        Assert.Contains(all, s => s.Id == b.Id);       // new id appended
    }

    [Fact]
    public async Task MergeByIdAsync_skips_blank_titles_and_returns_the_written_count()
    {
        var store = new JsonFileSongListStore(_dir);

        var written = await store.MergeByIdAsync([new SongListItem { Title = "   " }, new SongListItem { Title = "Real", Artist = "z" }]);

        Assert.Equal(1, written);
        Assert.Equal("Real", Assert.Single(await store.GetAllAsync()).Title);
    }

    [Fact]
    public async Task Profile_restore_lands_songs_in_the_new_singers_file_with_ids_preserved()
    {
        var singers = new JsonFileSingerStore(_dir);
        var session = new AppSession();

        // Mimic "add as a new singer": create the singer (id kept), switch to them, upsert their songs by id.
        var singer = new Singer { Id = Guid.NewGuid(), Name = "Jordan" };
        await singers.AddAsync(singer);
        session.SetActiveSinger(singer.Id);

        var song = new SongListItem { Id = Guid.NewGuid(), Title = "Bohemian Rhapsody", Artist = "Queen" };
        var written = await new JsonFileSongListStore(_dir, session).MergeByIdAsync([song]);

        Assert.Equal(1, written);
        Assert.True(File.Exists(_dir.FilePath($"song-list-{singer.Id:N}.json")));

        // A fresh store for the same active singer reads the song back with its original id.
        var reread = await new JsonFileSongListStore(_dir, session).GetAllAsync();
        Assert.Equal(song.Id, Assert.Single(reread).Id);
    }
}
