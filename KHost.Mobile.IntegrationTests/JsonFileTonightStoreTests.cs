using System.Text.Json;
using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

public sealed class JsonFileTonightStoreTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    private JsonFileTonightStore NewStore() => new(_dir);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task AddAsync_appends_in_order_and_ignores_a_song_already_queued()
    {
        var store = NewStore();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await store.AddAsync(a);
        await store.AddAsync(b);
        await store.AddAsync(a);   // already queued → no-op

        var all = await store.GetAllAsync();
        Assert.Equal([a, b], all.Select(e => e.SongId).ToArray());
        Assert.Equal([0, 1], all.Select(e => e.Order).ToArray());
    }

    [Fact]
    public async Task RemoveAsync_renumbers_the_remaining_entries_contiguously()
    {
        var store = NewStore();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        await store.AddAsync(a);
        await store.AddAsync(b);
        await store.AddAsync(c);

        await store.RemoveAsync(b);

        var all = await store.GetAllAsync();
        Assert.Equal([a, c], all.Select(e => e.SongId).ToArray());
        Assert.Equal([0, 1], all.Select(e => e.Order).ToArray());   // no gap where b was
    }

    [Fact]
    public async Task ReorderAsync_rewrites_the_set_order()
    {
        var store = NewStore();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        await store.AddAsync(a);
        await store.AddAsync(b);
        await store.AddAsync(c);

        await store.ReorderAsync([c, a, b]);

        var all = await store.GetAllAsync();
        Assert.Equal([c, a, b], all.Select(e => e.SongId).ToArray());
        Assert.Equal([0, 1, 2], all.Select(e => e.Order).ToArray());
    }

    [Fact]
    public async Task SetCompletedAsync_stamps_and_clears_the_completion_fields()
    {
        var store = NewStore();
        var song = Guid.NewGuid();
        var performance = Guid.NewGuid();
        await store.AddAsync(song);

        await store.SetCompletedAsync(song, completed: true, performanceId: performance);
        var done = Assert.Single(await store.GetAllAsync());
        Assert.True(done.Completed);
        Assert.NotNull(done.CompletedAt);
        Assert.Equal(performance, done.CompletedPerformanceId);

        await store.SetCompletedAsync(song, completed: false);
        var undone = Assert.Single(await store.GetAllAsync());
        Assert.False(undone.Completed);
        Assert.Null(undone.CompletedAt);
        Assert.Null(undone.CompletedPerformanceId);   // the logged-performance link is dropped on undo
    }

    [Fact]
    public async Task SetCompletedAsync_is_a_no_op_when_the_flag_is_unchanged()
    {
        var store = NewStore();
        var song = Guid.NewGuid();
        await store.AddAsync(song);
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.SetCompletedAsync(song, completed: true);
        Assert.Equal(1, fired);

        await store.SetCompletedAsync(song, completed: true);   // same value → no change
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task PruneAsync_drops_orphaned_entries_and_renumbers()
    {
        var store = NewStore();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        await store.AddAsync(a);
        await store.AddAsync(b);
        await store.AddAsync(c);

        await store.PruneAsync([a, c]);   // b's song no longer exists

        var all = await store.GetAllAsync();
        Assert.Equal([a, c], all.Select(e => e.SongId).ToArray());
        Assert.Equal([0, 1], all.Select(e => e.Order).ToArray());
    }

    [Fact]
    public async Task ClearAsync_empties_the_set_and_no_ops_when_already_empty()
    {
        var store = NewStore();
        await store.AddAsync(Guid.NewGuid());
        var fired = 0;
        store.Changed += (_, _) => fired++;

        await store.ClearAsync();
        Assert.Empty(await store.GetAllAsync());
        Assert.Equal(1, fired);

        await store.ClearAsync();   // already empty → no-op
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task State_persists_to_disk_and_is_read_back_by_a_fresh_instance()
    {
        var song = Guid.NewGuid();
        var writer = NewStore();
        await writer.AddAsync(song);
        await writer.SetCompletedAsync(song, completed: true);

        var reader = NewStore();
        var entry = Assert.Single(await reader.GetAllAsync());
        Assert.Equal(song, entry.SongId);
        Assert.True(entry.Completed);
    }

    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_set_rather_than_throwing()
    {
        await File.WriteAllTextAsync(_dir.FilePath("tonight.json"), "}not valid{");

        Assert.Empty(await NewStore().GetAllAsync());
    }

    [Fact]
    public async Task Deserializes_a_hand_written_file()
    {
        var song = Guid.NewGuid();
        var seeded = new List<TonightEntry> { new() { SongId = song, Order = 0, AddedAt = DateTimeOffset.Now } };
        await File.WriteAllTextAsync(
            _dir.FilePath("tonight.json"),
            JsonSerializer.Serialize(seeded, TonightJsonContext.Default.ListTonightEntry));

        var entry = Assert.Single(await NewStore().GetAllAsync());
        Assert.Equal(song, entry.SongId);
    }
}
