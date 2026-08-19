using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

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
    public async Task Switching_the_active_singer_raises_Changed_for_the_tonight_store_too()
    {
        var store = new JsonFileTonightStore(_dir, _session);
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
    public async Task With_no_session_the_store_uses_the_unsuffixed_file()
    {
        // The session-less path (integration tests / pre-seed) must still read/write the original single file.
        var store = new JsonFileSongListStore(_dir);
        await store.AddAsync("Africa", "Toto");

        Assert.True(File.Exists(_dir.FilePath("song-list.json")));
    }

    [Fact]
    public async Task A_singer_switch_landing_mid_operation_still_saves_to_the_singer_the_cache_was_loaded_for()
    {
        var mike = Guid.NewGuid();
        var sam = Guid.NewGuid();
        var store = new JsonFileSongListStore(_dir, new SwitchingSession(mike, sam));

        await store.AddAsync("Bohemian Rhapsody", "Queen");

        // The save must follow the key the load used, or the switch puts Mike's song in Sam's file.
        Assert.True(File.Exists(_dir.FilePath($"song-list-{mike:N}.json")));
        Assert.False(File.Exists(_dir.FilePath($"song-list-{sam:N}.json")));
    }

    [Fact]
    public async Task A_singer_switch_landing_mid_operation_still_saves_the_tonight_set_to_the_loaded_singer()
    {
        var mike = Guid.NewGuid();
        var sam = Guid.NewGuid();
        var store = new JsonFileTonightStore(_dir, new SwitchingSession(mike, sam));

        await store.AddAsync(Guid.NewGuid());

        Assert.True(File.Exists(_dir.FilePath($"tonight-{mike:N}.json")));
        Assert.False(File.Exists(_dir.FilePath($"tonight-{sam:N}.json")));
    }

    // One singer to the first reader, another to every reader after: a switch landing between load and save,
    // without the timing that makes a real one impossible to reproduce.
    private sealed class SwitchingSession(Guid first, Guid then) : IAppSession
    {
        private int _reads;

        public Guid? ActiveSingerId => _reads++ == 0 ? first : then;

        public bool LandingResolved { get; set; }
        public bool TutorialResolved { get; set; }
        public Guid? ActiveVenueId => null;
        public bool ActiveVenuePinned => false;
        public Guid? TutorialVenueDetailId => null;

        public event EventHandler? ActiveVenueChanged;
        public event EventHandler? ActiveSingerChanged;
        public event EventHandler? TutorialVenueDetailChanged;

        public void SetActiveVenue(Guid? venueId, bool pinned = false) => ActiveVenueChanged?.Invoke(this, EventArgs.Empty);
        public void SetActiveSinger(Guid? singerId) => ActiveSingerChanged?.Invoke(this, EventArgs.Empty);
        public void SetTutorialVenueDetail(Guid? venueId) => TutorialVenueDetailChanged?.Invoke(this, EventArgs.Empty);
        public void ClearMySongsView(Guid singerId) { }
        public MySongsViewState MySongsViewFor(Guid? singerId) => new();
    }
}
