using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ITonightStore"/> backed by a single JSON file in the app's private data directory — the same
/// durable-JSON pattern as <see cref="JsonFileSongListStore"/>. The in-memory list is the source of truth once
/// loaded; every mutation renumbers <see cref="TonightEntry.Order"/> to stay contiguous and rewrites the file. A
/// <see cref="SemaphoreSlim"/> guards against concurrent UI actions. A corrupt file is treated as an empty set.
/// </summary>
public sealed class JsonFileTonightStore : ITonightStore
{
    private readonly IAppDataDirectory _paths;
    private readonly IAppSession? _session;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _log;

    private List<TonightEntry>? _entries;
    private Guid? _loadedFor;   // the singer whose file _entries was loaded from (null = the legacy no-singer file)

    /// <summary>
    /// The Tonight set is per-singer: it reads/writes the active singer's file (<see cref="IAppSession.ActiveSingerId"/>).
    /// <paramref name="session"/> is optional so the integration tests can <c>new</c> the store without wiring a
    /// session — it then reads the single legacy file, exactly as before multi-singer support. The optional logger
    /// keeps those tests loggerless; DI supplies both.
    /// </summary>
    public JsonFileTonightStore(IAppDataDirectory paths, IAppSession? session = null, ILogger<JsonFileTonightStore>? logger = null)
    {
        _paths = paths;
        _session = session;
        _log = logger ?? NullLogger<JsonFileTonightStore>.Instance;
        if (_session is not null)
            _session.ActiveSingerChanged += OnActiveSingerChanged;
    }

    public event EventHandler? Changed;

    // A singer switch invalidates the cache (see LoadAsync's _loadedFor check) and must refresh every subscriber, so
    // re-raise Changed — the UI then reloads this singer's set exactly as it would after any mutation.
    private void OnActiveSingerChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    // The active singer's tonight file, or the legacy single-user file when no singer is active (pre-seed, or the
    // session-less test path).
    private string CurrentPath()
    {
        var id = _session?.ActiveSingerId;
        var name = id is null ? SingerDataFiles.LegacyTonight : SingerDataFiles.Tonight(id.Value);
        return Path.Combine(_paths.AppDataDirectory, name);
    }

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

    // Callers must hold _gate.
    private static void Renumber(List<TonightEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
            entries[i].Order = i;
    }

    // Callers must hold _gate.
    private async Task<List<TonightEntry>> LoadAsync()
    {
        var singer = _session?.ActiveSingerId;
        if (_entries is not null && _loadedFor == singer)
            return _entries;

        // First load, or the active singer changed out from under the cache → (re)load from that singer's file.
        _entries = null;
        _loadedFor = singer;
        var path = CurrentPath();

        if (!File.Exists(path))
        {
            _log.LogDebug("Tonight file not found at {Path}; starting with an empty set", path);
            return _entries = [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            _entries = await JsonSerializer.DeserializeAsync(stream, TonightJsonContext.Default.ListTonightEntry) ?? [];
            _log.LogDebug("Tonight set loaded: {Count} entries from {Path}", _entries.Count, path);
        }
        catch (JsonException ex)
        {
            // Corrupt file (e.g. an interrupted write) — start clean rather than crash the app.
            _log.LogWarning(ex, "Tonight file at {Path} is corrupt; starting with an empty set", path);
            _entries = [];
        }

        return _entries;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(List<TonightEntry> entries)
    {
        _entries = entries;
        _loadedFor = _session?.ActiveSingerId;
        var path = CurrentPath();
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, entries, TonightJsonContext.Default.ListTonightEntry);
        _log.LogDebug("Tonight set saved: {Count} entries to {Path}", entries.Count, path);
    }
}
