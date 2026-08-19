using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by a single JSON file in the app's private data directory — the same durable-JSON pattern as
/// <see cref="JsonFileTonightStore"/>. A corrupt file is quarantined and treated as an empty list. Read results
/// are sorted favorites-first then by name; storage order itself is insertion order.
/// </remarks>
internal sealed class JsonFileVenueStore : JsonFileStore<Venue>, IVenueStore
{
    private readonly string _filePath;

    // logger is optional so the integration tests can `new` the store without a logging stack; DI supplies the real one.
    public JsonFileVenueStore(IAppDataDirectory paths, ILogger<JsonFileVenueStore>? logger = null, IAtomicFileWriter? writer = null)
        : base(logger ?? NullLogger<JsonFileVenueStore>.Instance, writer)
        => _filePath = Path.Combine(paths.AppDataDirectory, "venues.json");

    protected override JsonTypeInfo<List<Venue>> TypeInfo => VenueJsonContext.Default.ListVenue;
    protected override string Label => "Venues";
    protected override string PathFor(Guid? key) => _filePath;

    public async Task<IReadOnlyList<Venue>> GetAllAsync()
    {
        await Gate.WaitAsync();
        try
        {
            return Sorted(await LoadAsync());
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Venue?> GetAsync(Guid id)
    {
        await Gate.WaitAsync();
        try
        {
            return (await LoadAsync()).FirstOrDefault(v => v.Id == id);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Venue> AddAsync(Venue venue)
    {
        ArgumentNullException.ThrowIfNull(venue);
        if (venue.Id == Guid.Empty)
            venue.Id = Guid.NewGuid();

        await Gate.WaitAsync();
        try
        {
            var venues = await LoadAsync();
            // An imported file carries the ids it was exported with. Two venues sharing one are indistinguishable
            // to UpdateAsync (edits the first) and RemoveAsync (deletes both), so the newcomer gets a fresh id.
            if (venues.Any(v => v.Id == venue.Id))
                venue.Id = Guid.NewGuid();
            venues.Add(venue);
            await SaveAsync(venues);
        }
        finally
        {
            Gate.Release();
        }

        RaiseChanged();
        return venue;
    }

    public async Task UpdateAsync(Venue venue)
    {
        ArgumentNullException.ThrowIfNull(venue);

        var changed = false;
        await Gate.WaitAsync();
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
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task RemoveAsync(Guid id)
    {
        var changed = false;
        await Gate.WaitAsync();
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
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    // Favorites first, then case-insensitive by name — the order the list and switcher want. A copy, so callers
    // can't mutate the cached list.
    private static List<Venue> Sorted(List<Venue> venues) =>
        venues
            .OrderByDescending(v => v.IsFavorite)
            .ThenBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

}
