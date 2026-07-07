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
public sealed class JsonFileSongListStore : ISongListStore
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
            return items.OrderByDescending(i => i.AddedAt).ToList();
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
            SungAt = sung ? DateTimeOffset.Now : null,
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

            items[index] = item;
            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.RemoveAll(i => i.Id == id) == 0)
                return;

            await SaveAsync(items);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

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
