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
/// <see cref="JsonFileVenueStore"/>. A corrupt file is quarantined and treated as an empty roster. Removing a
/// singer also deletes their personal data files so they don't orphan on disk.
/// </remarks>
internal sealed class JsonFileSingerStore : JsonFileStore<Singer>, ISingerStore
{
    private readonly ISingerFileNames _names;

    private readonly IAppDataDirectory _paths;
    private readonly string _filePath;

    // logger is optional so the integration tests can `new` the store without a logging stack; DI supplies the real one.
    public JsonFileSingerStore(IAppDataDirectory paths, ILogger<JsonFileSingerStore>? logger = null, IAtomicFileWriter? writer = null,
        ISingerFileNames? names = null)
        : base(logger ?? NullLogger<JsonFileSingerStore>.Instance, writer)
    {
        _names = names ?? new SingerFileNames();
        _paths = paths;
        _filePath = Path.Combine(paths.AppDataDirectory, "singers.json");
    }

    protected override JsonTypeInfo<List<Singer>> TypeInfo => SingerJsonContext.Default.ListSinger;
    protected override string Label => "Singers";
    protected override string PathFor(Guid? key) => _filePath;

    public async Task<IReadOnlyList<Singer>> GetAllAsync()
    {
        await Gate.WaitAsync();
        try
        {
            return Ordered(await LoadAsync());
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Singer?> GetAsync(Guid id)
    {
        await Gate.WaitAsync();
        try
        {
            return (await LoadAsync()).FirstOrDefault(s => s.Id == id);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Singer> AddAsync(Singer singer)
    {
        ArgumentNullException.ThrowIfNull(singer);
        if (singer.Id == Guid.Empty)
            singer.Id = Guid.NewGuid();

        await Gate.WaitAsync();
        try
        {
            var singers = await LoadAsync();
            // Append: sit past the current max order so a new singer lands at the end of the roster/switcher.
            singer.Order = singers.Count == 0 ? 0 : singers.Max(s => s.Order) + 1;
            singers.Add(singer);
            await SaveAsync(singers);
        }
        finally
        {
            Gate.Release();
        }

        RaiseChanged();
        return singer;
    }

    public async Task UpdateAsync(Singer singer)
    {
        ArgumentNullException.ThrowIfNull(singer);

        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var singers = await LoadAsync();
            var i = singers.FindIndex(s => s.Id == singer.Id);
            if (i < 0)
                return;

            singers[i] = singer;
            changed = true;
            await SaveAsync(singers);
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
            var singers = await LoadAsync();
            if (singers.RemoveAll(s => s.Id == id) == 0)
                return;

            changed = true;
            await SaveAsync(singers);
            // The singer is gone from the roster — clean up their personal data files so they don't orphan on disk.
            DeleteFile(_names.SongList(id));
            DeleteFile(_names.Tonight(id));
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task<Singer> EnsureSeededAsync()
    {
        Singer active;
        var seeded = false;
        await Gate.WaitAsync();
        try
        {
            var singers = await LoadAsync();
            if (singers.Count > 0)
                return Ordered(singers)[0];

            // Empty roster → a fresh install. Create the default singer to own this device's list.
            active = new Singer { Name = "Me", Color = SingerColors.Default, Order = 0 };
            singers.Add(active);
            await SaveAsync(singers);
            seeded = true;
            Log.LogInformation("Seeded the default singer {SingerId}", active.Id);
        }
        finally
        {
            Gate.Release();
        }

        if (seeded)
            RaiseChanged();
        return active;
    }

    // Best-effort delete of a removed singer's data file. A locked/absent file is harmless — don't fail the remove.
    private void DeleteFile(string name)
    {
        var path = Path.Combine(_paths.AppDataDirectory, name);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            Log.LogWarning(ex, "Couldn't delete removed singer's file {Path}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.LogWarning(ex, "Couldn't delete removed singer's file {Path}", path);
        }
    }

    // By explicit Order then add time — the order the roster and switcher want. A copy, so callers can't mutate cache.
    private static List<Singer> Ordered(List<Singer> singers) =>
        singers.OrderBy(s => s.Order).ThenBy(s => s.AddedAt).ToList();

}
