using System.Text.Json;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ITonightStore"/> backed by a single JSON file in the app's private data directory — the same
/// durable-JSON pattern as <see cref="JsonFileSongListStore"/>. The in-memory list is the source of truth once
/// loaded; every mutation renumbers <see cref="TonightEntry.Order"/> to stay contiguous and rewrites the file. A
/// <see cref="SemaphoreSlim"/> guards against concurrent UI actions. A corrupt file is treated as an empty set.
/// </summary>
public sealed class JsonFileTonightStore(IAppDataDirectory paths) : ITonightStore
{
    private readonly string _filePath = Path.Combine(paths.AppDataDirectory, "tonight.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<TonightEntry>? _entries;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<TonightEntry>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return (await LoadAsync()).OrderBy(e => e.Order).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(Guid songId)
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            if (entries.Any(e => e.SongId == songId))
                return;   // already queued

            entries.Add(new TonightEntry
            {
                SongId = songId,
                Order = entries.Count,
                AddedAt = DateTimeOffset.Now,
            });
            Renumber(entries);
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveAsync(Guid songId)
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            if (entries.RemoveAll(e => e.SongId == songId) == 0)
                return;

            Renumber(entries);
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedSongIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSongIds);

        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();

            // Rank each queued song by its index in the requested order; anything not mentioned sinks to the
            // end keeping its previous relative order (defensive — the UI always sends the full set).
            var rank = new Dictionary<Guid, int>();
            for (var i = 0; i < orderedSongIds.Count; i++)
                rank[orderedSongIds[i]] = i;

            var reordered = entries
                .OrderBy(e => rank.TryGetValue(e.SongId, out var r) ? r : int.MaxValue)
                .ThenBy(e => e.Order)
                .ToList();

            Renumber(reordered);
            await SaveAsync(reordered);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetCompletedAsync(Guid songId, bool completed, Guid? performanceId = null)
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            var entry = entries.FirstOrDefault(e => e.SongId == songId);
            if (entry is null || entry.Completed == completed)
                return;

            entry.Completed = completed;
            entry.CompletedAt = completed ? DateTimeOffset.Now : null;
            entry.CompletedPerformanceId = completed ? performanceId : null;
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            if (entries.Count == 0)
                return;

            entries.Clear();
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task PruneAsync(IReadOnlyCollection<Guid> existingSongIds)
    {
        ArgumentNullException.ThrowIfNull(existingSongIds);

        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            var live = new HashSet<Guid>(existingSongIds);
            if (entries.RemoveAll(e => !live.Contains(e.SongId)) == 0)
                return;

            Renumber(entries);
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    // Make Order 0..n-1 contiguous in the list's current sequence. Callers must hold _gate.
    private static void Renumber(List<TonightEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
            entries[i].Order = i;
    }

    // Callers must hold _gate.
    private async Task<List<TonightEntry>> LoadAsync()
    {
        if (_entries is not null)
            return _entries;

        if (!File.Exists(_filePath))
            return _entries = [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _entries = await JsonSerializer.DeserializeAsync(stream, TonightJsonContext.Default.ListTonightEntry) ?? [];
        }
        catch (JsonException)
        {
            // Corrupt file (e.g. an interrupted write) — start clean rather than crash the app.
            _entries = [];
        }

        return _entries;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(List<TonightEntry> entries)
    {
        _entries = entries;
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, entries, TonightJsonContext.Default.ListTonightEntry);
    }
}
