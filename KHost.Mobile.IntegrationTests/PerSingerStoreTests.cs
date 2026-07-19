using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

/// <summary>
/// The per-singer namespacing of the song-list / tonight stores: with an <see cref="AppSession"/> wired in, each
/// store reads/writes the active singer's own file, and switching the active singer swaps which data is seen and
/// raises <c>Changed</c> so the UI reloads.
/// </summary>
public sealed class PerSingerStoreTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();
    private readonly AppSession _session = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Each_singer_has_their_own_song_list()
    {
        var mike = Guid.NewGuid();
        var sam = Guid.NewGuid();
        var store = new JsonFileSongListStore(_dir, _session);

        _session.SetActiveSinger(mike);
        await store.AddAsync("Bohemian Rhapsody", "Queen");

        _session.SetActiveSinger(sam);
        Assert.Empty(await store.GetAllAsync());          // Sam's list is separate — empty
        await store.AddAsync("Dancing Queen", "ABBA");

        _session.SetActiveSinger(mike);
        var mikeSongs = await store.GetAllAsync();         // Mike's song is still there
        Assert.Equal("Bohemian Rhapsody", Assert.Single(mikeSongs).Title);

        // Each singer's data lives in its own on-disk file.
        Assert.True(File.Exists(_dir.FilePath($"song-list-{mike:N}.json")));
        Assert.True(File.Exists(_dir.FilePath($"song-list-{sam:N}.json")));
    }

    [Fact]
    public async Task Switching_the_active_singer_raises_Changed_so_the_ui_reloads()
    {
        var store = new JsonFileSongListStore(_dir, _session);
        _session.SetActiveSinger(Guid.NewGuid());
        var fired = 0;
        store.Changed += (_, _) => fired++;

        _session.SetActiveSinger(Guid.NewGuid());   // a real switch → fires
        Assert.Equal(1, fired);

        var same = _session.ActiveSingerId!.Value;
        _session.SetActiveSinger(same);             // no change → no fire
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task Tonight_set_is_also_per_singer()
    {
        var mike = Guid.NewGuid();
        var sam = Guid.NewGuid();
        var store = new JsonFileTonightStore(_dir, _session);
        var song = Guid.NewGuid();

        _session.SetActiveSinger(mike);
        await store.AddAsync(song);
        Assert.Single(await store.GetAllAsync());

        _session.SetActiveSinger(sam);
        Assert.Empty(await store.GetAllAsync());    // Sam's Tonight set is separate
    }

    [Fact]
    public async Task With_no_session_the_store_uses_the_legacy_file()
    {
        // The session-less path (integration tests / pre-seed) must still read/write the original single file.
        var store = new JsonFileSongListStore(_dir);
        await store.AddAsync("Africa", "Toto");

        Assert.True(File.Exists(_dir.FilePath("song-list.json")));
    }
}
