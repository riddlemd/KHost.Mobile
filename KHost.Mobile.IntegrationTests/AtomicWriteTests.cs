using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

// Crash-safety of the store writes, exercised through the real song-list store (session-less → the legacy file).
// AtomicFile is internal, so its behavior is verified via the observable store path rather than directly.
public sealed class AtomicWriteTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task A_save_leaves_no_tmp_file_behind()
    {
        var store = new JsonFileSongListStore(_dir);

        await store.AddAsync("Bohemian Rhapsody", "Queen");

        Assert.True(File.Exists(_dir.FilePath("song-list.json")));
        Assert.False(File.Exists(_dir.FilePath("song-list.json.tmp")));   // the temp sibling was renamed away, not left
    }

    [Fact]
    public async Task A_corrupt_file_is_quarantined_rather_than_erased()
    {
        var path = _dir.FilePath("song-list.json");
        await File.WriteAllTextAsync(path, "}garbage{");   // e.g. a pre-atomic-write interrupted save

        var store = new JsonFileSongListStore(_dir);
        Assert.Empty(await store.GetAllAsync());            // recovers to empty rather than crashing

        Assert.False(File.Exists(path));                   // the bad file was moved aside...
        Assert.True(File.Exists(path + ".corrupt"));       // ...to a .corrupt sibling...
        Assert.Equal("}garbage{", await File.ReadAllTextAsync(path + ".corrupt"));   // ...with its bytes intact for recovery
    }

    [Fact]
    public async Task A_save_after_recovering_from_corruption_writes_a_clean_readable_file()
    {
        var path = _dir.FilePath("song-list.json");
        await File.WriteAllTextAsync(path, "}garbage{");

        var store = new JsonFileSongListStore(_dir);
        await store.GetAllAsync();                          // quarantines the bad file
        await store.AddAsync("Africa", "Toto");             // writes a fresh, valid file at the same path

        var reread = await new JsonFileSongListStore(_dir).GetAllAsync();
        Assert.Equal("Africa", Assert.Single(reread).Title);
    }
}
