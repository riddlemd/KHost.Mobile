using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Logic;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by a single JSON file in the app's private data directory — the same durable-JSON pattern as
/// <see cref="JsonFileSongListStore"/>. Every mutation renumbers <see cref="TonightEntry.Order"/> to stay
/// contiguous and rewrites the file, under a <see cref="SemaphoreSlim"/>. A corrupt file is quarantined and
/// treated as an empty set.
/// </remarks>
public sealed class JsonFileTonightStore : JsonFileStore<TonightEntry>, ITonightStore
{
    private readonly IAppDataDirectory _paths;
    private readonly IAppSession? _session;
    // GetLocalNow, not GetUtcNow: every stored timestamp in this app is local, and switching would shift every
    // new entry by the UTC offset against the ones already on the device.
    private readonly TimeProvider _clock;


    /// <summary>
    /// The Tonight set is per-singer: it reads/writes the active singer's file (<see cref="IAppSession.ActiveSingerId"/>).
    /// <paramref name="session"/> and <paramref name="logger"/> are optional so the integration tests can <c>new</c>
    /// the store bare; with no session it falls back to the single legacy file. DI supplies both.
    /// </summary>
    public JsonFileTonightStore(
        IAppDataDirectory paths,
        IAppSession? session = null,
        ILogger<JsonFileTonightStore>? logger = null,
        TimeProvider? timeProvider = null)
        : base(logger ?? NullLogger<JsonFileTonightStore>.Instance)
    {
        _paths = paths;
        _session = session;
        _clock = timeProvider ?? TimeProvider.System;
        if (_session is not null)
            _session.ActiveSingerChanged += OnActiveSingerChanged;
    }

    // A singer switch invalidates the cache (see LoadAsync's _loadedFor check) and must refresh every subscriber, so
    // re-raise Changed — the UI then reloads this singer's set exactly as it would after any mutation.
    private void OnActiveSingerChanged(object? sender, EventArgs e) => RaiseChanged();

    // The given singer's tonight file, or the legacy single-user file when no singer is active (pre-seed, or the
    // session-less test path). Takes the singer explicitly — LoadAsync captures ActiveSingerId ONCE and SaveAsync
    // writes to the singer the data was LOADED for, so a singer switch landing mid-operation can't write one
    // singer's set into another singer's file.
    protected override JsonTypeInfo<List<TonightEntry>> TypeInfo => TonightJsonContext.Default.ListTonightEntry;
    protected override string Label => "Tonight set";
    protected override Guid? CurrentKey => _session?.ActiveSingerId;

    protected override string PathFor(Guid? singerId)
    {
        var name = singerId is null ? SingerDataFiles.LegacyTonight : SingerDataFiles.Tonight(singerId.Value);
        return Path.Combine(_paths.AppDataDirectory, name);
    }

    public async Task<IReadOnlyList<TonightEntry>> GetAllAsync()
    {
        await Gate.WaitAsync();
        try
        {
            return (await LoadAsync()).OrderBy(e => e.Order).ToList();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task AddAsync(Guid songId)
    {
        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            if (entries.Any(e => e.SongId == songId))
                return;   // already queued

            entries.Add(new TonightEntry
            {
                SongId = songId,
                Order = entries.Count,
                AddedAt = _clock.GetLocalNow(),
            });
            Renumber(entries);
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task RemoveAsync(Guid songId)
    {
        var changed = false;
        await Gate.WaitAsync();
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
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedSongIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSongIds);

        await Gate.WaitAsync();
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
            Gate.Release();
        }

        RaiseChanged();
    }

    public async Task SetCompletedAsync(Guid songId, bool completed, Guid? performanceId = null)
    {
        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            var entry = entries.FirstOrDefault(e => e.SongId == songId);
            if (entry is null || entry.Completed == completed)
                return;

            entry.Completed = completed;
            entry.CompletedAt = completed ? _clock.GetLocalNow() : null;
            entry.CompletedPerformanceId = completed ? performanceId : null;
            changed = true;
            await SaveAsync(entries);
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await Gate.WaitAsync();
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
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task PruneAsync(IReadOnlyCollection<Guid> existingSongIds)
    {
        ArgumentNullException.ThrowIfNull(existingSongIds);

        var changed = false;
        await Gate.WaitAsync();
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
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    // Callers must hold Gate.
    private static void Renumber(List<TonightEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
            entries[i].Order = i;
    }
}
