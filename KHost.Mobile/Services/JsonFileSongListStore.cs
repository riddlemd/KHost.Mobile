using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ISongListStore"/> backed by a single JSON file in the app's private data directory. Mirrors the KHost
/// desktop's "durable JSON cache" pattern for small, frequently-mutated state. The in-memory list is the source of
/// truth once loaded; every mutation rewrites the file. Guarded by a <see cref="SemaphoreSlim"/> so concurrent UI
/// actions can't corrupt the file or the cache.
/// </summary>
public sealed class JsonFileSongListStore : ILocalSongStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "song-list.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<SongListItem>? _items;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<SongListItem>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            // Tombstones (soft-deleted items kept for cloud-sync propagation) are invisible to the UI.
            return items.Where(i => i.DeletedAt is null)
                        .OrderByDescending(i => i.AddedAt)
                        .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SongListItem> AddAsync(string title, string artist, string? notes = null, string? genre = null, int confidence = 0, int? year = null)
    {
        var rating = Math.Clamp(confidence, 0, 5);
        var sung = rating >= 1;
        var item = new SongListItem
        {
            Title = title.Trim(),
            Artist = artist.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Genre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
            Year = year,
            Status = sung ? SongListItemStatus.Sang : SongListItemStatus.WantToSing,
            SungDates = sung ? [DateTimeOffset.Now] : [],
            Confidence = rating,
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

            item.UpdatedAt = DateTimeOffset.Now;   // stamp the edit for last-write-wins cloud sync
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

                item.UpdatedAt = DateTimeOffset.Now;   // stamp each edit for last-write-wins cloud sync
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
        var now = DateTimeOffset.Now;
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            // Soft-delete: keep the row as a tombstone so the deletion propagates to the user's other
            // same-ecosystem devices on the next sync instead of the row silently reappearing.
            var item = items.FirstOrDefault(i => i.Id == id && i.DeletedAt is null);
            if (item is null)
                return;

            item.DeletedAt = now;
            item.UpdatedAt = now;
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
        var now = DateTimeOffset.Now;
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            var existing = items.FirstOrDefault(i => i.Id == item.Id);
            if (existing is not null)
            {
                if (existing.DeletedAt is null)
                    return;   // already live — e.g. a double Undo

                // Undo of a soft-delete: clear the tombstone in place. Re-adding would hit the
                // "already present" case (the tombstone is still in the list) and no-op the Undo.
                existing.DeletedAt = null;
                existing.UpdatedAt = now;
            }
            else
            {
                // Tombstone was compacted away between remove and Undo — resurrect the captured copy.
                item.DeletedAt = null;
                item.UpdatedAt = now;
                items.Add(item);
            }

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

            // Seed the dedupe set from LIVE items only — a title that was previously deleted (tombstoned)
            // is re-importable, so it comes back as a fresh live entry. Add() also catches repeats within the batch.
            var seen = skipDuplicates
                ? new HashSet<string>(items.Where(i => i.DeletedAt is null).Select(DedupeKey), StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var item in incoming)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Title))
                    continue;

                item.Title = item.Title.Trim();
                item.Artist = item.Artist.Trim();

                if (seen is not null && !seen.Add(DedupeKey(item)))
                    continue;   // already in the list, or a duplicate earlier in this batch

                item.UpdatedAt = DateTimeOffset.Now;   // stamp for last-write-wins cloud sync
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

        if (added > 0)
            Changed?.Invoke(this, EventArgs.Empty);

        return added;
    }

    public async Task<IReadOnlyList<SongListItem>> GetRawAsync()
    {
        await _gate.WaitAsync();
        try
        {
            // A copy so the coordinator can merge without racing a concurrent mutation of the cached list.
            return (await LoadAsync()).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyMergedAsync(IReadOnlyList<SongListItem> syncResult)
    {
        ArgumentNullException.ThrowIfNull(syncResult);

        var changed = false;
        await _gate.WaitAsync();
        try
        {
            // Merge the sync result INTO the live snapshot (not overwrite it): any edit the user made while the
            // network pull was in flight is in `current` with a newer UpdatedAt, so it survives the union and the
            // pending debounced sync pushes it. Holding _gate makes the read-merge-write atomic w.r.t. other edits.
            var current = await LoadAsync();
            var final = SyncMerge.Compact(SyncMerge.Merge(current, syncResult), DateTimeOffset.Now);
            if (SyncMerge.Signature(current) == SyncMerge.Signature(final))
                return;   // converged — nothing to write, no UI churn

            await SaveAsync(final.ToList());
            changed = true;
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    // Title+artist identity used for de-duplication (trimmed, case-insensitive). The  unit
    // separator keeps "AB"+"C" distinct from "A"+"BC". Mirrors the add-form duplicate guard in MySongs.
    private static string DedupeKey(SongListItem item)
        => $"{item.Title.Trim()}{item.Artist.Trim()}";

    // Callers must hold _gate.
    private async Task<List<SongListItem>> LoadAsync()
    {
        if (_items is not null)
            return _items;

        if (!File.Exists(_filePath))
            return _items = [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _items = await JsonSerializer.DeserializeAsync<List<SongListItem>>(stream) ?? [];
        }
        catch (JsonException)
        {
            // Corrupt file (e.g. interrupted write on a prior version) — start clean rather than crash the app.
            _items = [];
        }

        return _items;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(List<SongListItem> items)
    {
        _items = items;
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, SerializerOptions);
    }
}
