using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ISongListStore"/> backed by a single JSON file in the app's private data directory. Mirrors the KHost
/// desktop's "durable JSON cache" pattern for small, frequently-mutated state. The in-memory list is the source of
/// truth once loaded; every mutation rewrites the file. Guarded by a <see cref="SemaphoreSlim"/> so concurrent UI
/// actions can't corrupt the file or the cache.
/// </summary>
public sealed class JsonFileSongListStore : ISongListStore
{
    private readonly IAppDataDirectory _paths;
    private readonly IAppSession? _session;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _log;

    private List<SongListItem>? _items;
    private Guid? _loadedFor;   // the singer whose file _items was loaded from (null = the legacy no-singer file)

    /// <summary>
    /// The song list is per-singer: it reads/writes the active singer's file (<see cref="IAppSession.ActiveSingerId"/>).
    /// <paramref name="session"/> is optional so the integration tests can <c>new</c> the store without wiring a
    /// session — it then reads the single legacy file, exactly as before multi-singer support. The optional logger
    /// keeps those tests loggerless; DI supplies both.
    /// </summary>
    public JsonFileSongListStore(IAppDataDirectory paths, IAppSession? session = null, ILogger<JsonFileSongListStore>? logger = null)
    {
        _paths = paths;
        _session = session;
        _log = logger ?? NullLogger<JsonFileSongListStore>.Instance;
        if (_session is not null)
            _session.ActiveSingerChanged += OnActiveSingerChanged;
    }

    public event EventHandler? Changed;

    // A singer switch invalidates the cache (see LoadAsync's _loadedFor check) and must refresh every subscriber, so
    // re-raise Changed — the UI then reloads this singer's list exactly as it would after any mutation.
    private void OnActiveSingerChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    // The given singer's song-list file, or the legacy single-user file when no singer is active (pre-seed, or the
    // session-less test path). Takes the singer explicitly — LoadAsync captures ActiveSingerId ONCE and SaveAsync
    // writes to the singer the data was LOADED for, so a singer switch landing mid-operation (between LoadAsync's
    // await and the save) can't write one singer's list into another singer's file.
    private string PathFor(Guid? singerId)
    {
        var name = singerId is null ? SingerDataFiles.LegacySongList : SingerDataFiles.SongList(singerId.Value);
        return Path.Combine(_paths.AppDataDirectory, name);
    }

    public async Task<IReadOnlyList<SongListItem>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            return items.OrderByDescending(i => i.AddedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SongListItem> AddAsync(string title, string artist, string? notes = null, string? genre = null, int? year = null)
    {
        var item = new SongListItem
        {
            Title = title.Trim(),
            Artist = artist.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Genre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
            Year = year,
            Status = SongListItemStatus.WantToSing,   // new songs start on the wishlist; sung later from the detail sheet
        };

        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            items.Add(item);
            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public async Task UpdateAsync(SongListItem item)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            var index = items.FindIndex(i => i.Id == item.Id);
            if (index < 0)
                return;

            items[index] = item;
            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateRangeAsync(IEnumerable<SongListItem> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            foreach (var item in incoming)
            {
                var index = items.FindIndex(i => i.Id == item.Id);
                if (index < 0)
                    continue;

                items[index] = item;
                changed = true;
            }

            if (changed)
                await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveAsync(Guid id)
    {
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.RemoveAll(i => i.Id == id) == 0)
                return;

            changed = true;
            await SaveAsync(items);
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
            var items = await LoadAsync();
            if (items.Count == 0)
                return;

            items.Clear();
            changed = true;
            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RestoreAsync(SongListItem item)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.Any(i => i.Id == item.Id))
                return;   // already present — e.g. a double Undo

            // Undo of a removal: re-add the captured copy in its original position by AddedAt ordering.
            items.Add(item);
            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> ImportAsync(IEnumerable<SongListItem> incoming, bool skipDuplicates = true)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var added = 0;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();

            // Seed the dedupe set from the existing list. Add() also catches repeats within the batch.
            var seen = skipDuplicates
                ? new HashSet<string>(items.Select(DedupeKey), StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var item in incoming)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Title))
                    continue;

                item.Title = item.Title.Trim();
                item.Artist = item.Artist.Trim();

                if (seen is not null && !seen.Add(DedupeKey(item)))
                    continue;   // already in the list, or a duplicate earlier in this batch

                MigrateToPerformances(item);   // fold a legacy-format import (old Cue export) into Performances
                items.Add(item);
                added++;
            }

            if (added > 0)
                await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        _log.LogInformation("Song list import: added {Added} songs (skipDuplicates={SkipDuplicates})", added, skipDuplicates);
        if (added > 0)
            Changed?.Invoke(this, EventArgs.Empty);

        return added;
    }

    public async Task<int> MergeByIdAsync(IEnumerable<SongListItem> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var written = 0;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            foreach (var item in incoming)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Title))
                    continue;

                MigrateToPerformances(item);   // fold a legacy-format profile into Performances if needed
                var index = items.FindIndex(i => i.Id == item.Id);
                if (index < 0)
                    items.Add(item);            // new id → append
                else
                    items[index] = item;        // existing id → replace verbatim (restore)
                written++;
            }

            if (written > 0)
                await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        _log.LogInformation("Song list profile-restore: wrote {Written} songs by id", written);
        if (written > 0)
            Changed?.Invoke(this, EventArgs.Empty);

        return written;
    }

    // Title+artist identity used for de-duplication (trimmed, case-insensitive). The  unit
    // separator keeps "AB"+"C" distinct from "A"+"BC". Mirrors the add-form duplicate guard in MySongs.
    private static string DedupeKey(SongListItem item)
        => $"{item.Title.Trim()}{item.Artist.Trim()}";

    // Callers must hold _gate.
    private async Task<List<SongListItem>> LoadAsync()
    {
        var singer = _session?.ActiveSingerId;
        if (_items is not null && _loadedFor == singer)
            return _items;

        // First load, or the active singer changed out from under the cache → (re)load from that singer's file.
        _items = null;
        _loadedFor = singer;
        var path = PathFor(singer);

        if (!File.Exists(path))
        {
            _log.LogDebug("Song list file not found at {Path}; starting with an empty list", path);
            return _items = [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            _items = await JsonSerializer.DeserializeAsync(stream, SongListJsonContext.Default.ListSongListItem) ?? [];
            _log.LogDebug("Song list loaded: {Count} songs from {Path}", _items.Count, path);
        }
        catch (JsonException ex)
        {
            // Corrupt file (e.g. interrupted write on a pre-atomic-write version) — quarantine it aside so the bad
            // bytes aren't erased by the next save, then start clean rather than crash the app.
            _log.LogWarning(ex, "Song list file at {Path} is corrupt; quarantining it and starting with an empty list", path);
            AtomicFile.Quarantine(path);
            _items = [];
        }

        // One-time migration from the pre-per-performance shape (SungDates + single Confidence) into Performances.
        // Runs only on first load; persists the result so later launches read the already-migrated file.
        var migrated = false;
        foreach (var item in _items)
            migrated |= MigrateToPerformances(item);

        if (migrated)
            await SaveAsync(_items);

        return _items;
    }

    // Fold a legacy song into the Performances model: each old sung timestamp becomes a Performance carrying the
    // song's old single rating; a rated-but-dateless legacy entry gets one performance stamped at AddedAt. The legacy
    // fields are then emptied so new writes stop carrying them. Returns true if anything changed. No-op once migrated.
    private static bool MigrateToPerformances(SongListItem item)
    {
        if (item.Performances.Count > 0)
            return false;
        if (item.SungDates.Count == 0 && item.Confidence == 0)
            return false;

        var rating = Math.Clamp(item.Confidence, 0, 5);
        if (item.SungDates.Count > 0)
        {
            foreach (var date in item.SungDates)
                item.Performances.Add(new Performance { Date = date, HowItWent = rating });
        }
        else
        {
            // Rated with no recorded date (shouldn't normally happen) — anchor a single performance at AddedAt.
            item.Performances.Add(new Performance { Date = item.AddedAt, HowItWent = rating });
        }

        item.Status = item.Performances.Count > 0 ? SongListItemStatus.Sang : SongListItemStatus.WantToSing;
        item.SungDates = [];
        item.Confidence = 0;
        return true;
    }

    // Callers must hold _gate. Writes to the singer the items were LOADED for (_loadedFor, set by LoadAsync before
    // any await) — never re-reads ActiveSingerId, which may have moved on while this operation was in flight.
    private async Task SaveAsync(List<SongListItem> items)
    {
        _items = items;
        var path = PathFor(_loadedFor);
        await AtomicFile.WriteAsync(path, stream => JsonSerializer.SerializeAsync(stream, items, SongListJsonContext.Default.ListSongListItem));
        _log.LogDebug("Song list saved: {Count} songs to {Path}", items.Count, path);
    }
}
