using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IVenueStore"/> backed by a single JSON file in the app's private data directory — the same
/// durable-JSON pattern as <see cref="JsonFileTonightStore"/>. The in-memory list is the source of truth once
/// loaded; a <see cref="SemaphoreSlim"/> guards against concurrent UI actions. A corrupt file is treated as an
/// empty list. Read results are sorted favorites-first then by name; storage order itself is insertion order.
/// </summary>
public sealed class JsonFileVenueStore(IAppDataDirectory paths, ILogger<JsonFileVenueStore>? logger = null) : IVenueStore
{
    private readonly string _filePath = Path.Combine(paths.AppDataDirectory, "venues.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Optional so the integration tests can `new` the store without a logging stack; DI supplies the real logger.
    private readonly ILogger _log = logger ?? NullLogger<JsonFileVenueStore>.Instance;

    private List<Venue>? _venues;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<Venue>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return Sorted(await LoadAsync());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Venue?> GetAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            return (await LoadAsync()).FirstOrDefault(v => v.Id == id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Venue> AddAsync(Venue venue)
    {
        ArgumentNullException.ThrowIfNull(venue);
        if (venue.Id == Guid.Empty)
            venue.Id = Guid.NewGuid();

        await _gate.WaitAsync();
        try
        {
            var venues = await LoadAsync();
            venues.Add(venue);
            await SaveAsync(venues);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return venue;
    }

    public async Task UpdateAsync(Venue venue)
    {
        ArgumentNullException.ThrowIfNull(venue);

        var changed = false;
        await _gate.WaitAsync();
        try
        {
            var venues = await LoadAsync();
            var i = venues.FindIndex(v => v.Id == venue.Id);
            if (i < 0)
                return;

            venues[i] = venue;
            changed = true;
            await SaveAsync(venues);
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
            var venues = await LoadAsync();
            if (venues.RemoveAll(v => v.Id == id) == 0)
                return;

            changed = true;
            await SaveAsync(venues);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    // Favorites first, then case-insensitive by name — the order the list and switcher want. A copy, so callers
    // can't mutate the cached list.
    private static List<Venue> Sorted(List<Venue> venues) =>
        venues
            .OrderByDescending(v => v.IsFavorite)
            .ThenBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    // Callers must hold _gate.
    private async Task<List<Venue>> LoadAsync()
    {
        if (_venues is not null)
            return _venues;

        if (!File.Exists(_filePath))
        {
            _log.LogDebug("Venues file not found at {Path}; starting with an empty list", _filePath);
            return _venues = [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _venues = await JsonSerializer.DeserializeAsync(stream, VenueJsonContext.Default.ListVenue) ?? [];
            _log.LogDebug("Venues loaded: {Count} from {Path}", _venues.Count, _filePath);
        }
        catch (JsonException ex)
        {
            // Corrupt file (e.g. an interrupted write) — start clean rather than crash the app.
            _log.LogWarning(ex, "Venues file at {Path} is corrupt; starting with an empty list", _filePath);
            _venues = [];
        }

        return _venues;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(List<Venue> venues)
    {
        _venues = venues;
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, venues, VenueJsonContext.Default.ListVenue);
        _log.LogDebug("Venues saved: {Count} to {Path}", venues.Count, _filePath);
    }
}
