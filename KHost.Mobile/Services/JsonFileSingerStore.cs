using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ISingerStore"/> backed by a single JSON file in the app's private data directory — the same
/// durable-JSON pattern as <see cref="JsonFileVenueStore"/>. The in-memory list is the source of truth once loaded;
/// a <see cref="SemaphoreSlim"/> guards against concurrent UI actions. A corrupt file is treated as an empty
/// roster. Read results are ordered by <see cref="Singer.Order"/>; a removed singer's personal data files are
/// deleted so they don't orphan on disk.
/// </summary>
public sealed class JsonFileSingerStore(IAppDataDirectory paths, ILogger<JsonFileSingerStore>? logger = null) : ISingerStore
{
    private readonly string _filePath = Path.Combine(paths.AppDataDirectory, "singers.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Optional so the integration tests can `new` the store without a logging stack; DI supplies the real logger.
    private readonly ILogger _log = logger ?? NullLogger<JsonFileSingerStore>.Instance;

    private List<Singer>? _singers;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<Singer>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return Ordered(await LoadAsync());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Singer?> GetAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            return (await LoadAsync()).FirstOrDefault(s => s.Id == id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Singer> AddAsync(Singer singer)
    {
        ArgumentNullException.ThrowIfNull(singer);
        if (singer.Id == Guid.Empty)
            singer.Id = Guid.NewGuid();

        await _gate.WaitAsync();
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
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return singer;
    }

    public async Task UpdateAsync(Singer singer)
    {
        ArgumentNullException.ThrowIfNull(singer);

        var changed = false;
        await _gate.WaitAsync();
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
            var singers = await LoadAsync();
            if (singers.RemoveAll(s => s.Id == id) == 0)
                return;

            changed = true;
            await SaveAsync(singers);
            // The singer is gone from the roster — clean up their personal data files so they don't orphan on disk.
            DeleteFile(SingerDataFiles.SongList(id));
            DeleteFile(SingerDataFiles.Tonight(id));
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<Singer> EnsureSeededAsync()
    {
        Singer active;
        var seeded = false;
        await _gate.WaitAsync();
        try
        {
            var singers = await LoadAsync();
            if (singers.Count > 0)
                return Ordered(singers)[0];

            // Empty roster → first launch with the feature (or a fresh install). Create the default singer and fold
            // any pre-existing single-user list into it, so an upgrader keeps their songs as this singer's list.
            active = new Singer { Name = "Me", Color = SingerColors.Default, Order = 0 };
            MigrateLegacyFile(SingerDataFiles.LegacySongList, SingerDataFiles.SongList(active.Id));
            MigrateLegacyFile(SingerDataFiles.LegacyTonight, SingerDataFiles.Tonight(active.Id));
            singers.Add(active);
            await SaveAsync(singers);
            seeded = true;
            _log.LogInformation("Seeded the default singer {SingerId} and migrated any legacy single-user files", active.Id);
        }
        finally
        {
            _gate.Release();
        }

        if (seeded)
            Changed?.Invoke(this, EventArgs.Empty);
        return active;
    }

    // Move a legacy single-user file into a singer's namespaced file, once. Only runs when the source exists and the
    // destination doesn't (so it never clobbers an already-migrated file). Best-effort: a failed move just means the
    // upgrader starts that singer empty rather than crashing the seed. Callers hold _gate.
    private void MigrateLegacyFile(string legacyName, string singerName)
    {
        var src = Path.Combine(paths.AppDataDirectory, legacyName);
        var dst = Path.Combine(paths.AppDataDirectory, singerName);
        if (!File.Exists(src) || File.Exists(dst))
            return;

        try
        {
            File.Move(src, dst);
            _log.LogInformation("Migrated legacy {Legacy} into {Singer}", legacyName, singerName);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Couldn't migrate legacy {Legacy}; the singer will start empty", legacyName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Couldn't migrate legacy {Legacy}; the singer will start empty", legacyName);
        }
    }

    // Best-effort delete of a removed singer's data file. A locked/absent file is harmless — don't fail the remove.
    private void DeleteFile(string name)
    {
        var path = Path.Combine(paths.AppDataDirectory, name);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Couldn't delete removed singer's file {Path}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning(ex, "Couldn't delete removed singer's file {Path}", path);
        }
    }

    // By explicit Order then add time — the order the roster and switcher want. A copy, so callers can't mutate cache.
    private static List<Singer> Ordered(List<Singer> singers) =>
        singers.OrderBy(s => s.Order).ThenBy(s => s.AddedAt).ToList();

    // Callers must hold _gate.
    private async Task<List<Singer>> LoadAsync()
    {
        if (_singers is not null)
            return _singers;

        if (!File.Exists(_filePath))
        {
            _log.LogDebug("Singers file not found at {Path}; starting with an empty roster", _filePath);
            return _singers = [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _singers = await JsonSerializer.DeserializeAsync(stream, SingerJsonContext.Default.ListSinger) ?? [];
            _log.LogDebug("Singers loaded: {Count} from {Path}", _singers.Count, _filePath);
        }
        catch (JsonException ex)
        {
            // Corrupt file — quarantine the bad bytes aside, then start clean rather than crash the app.
            _log.LogWarning(ex, "Singers file at {Path} is corrupt; quarantining it and starting with an empty roster", _filePath);
            AtomicFile.Quarantine(_filePath);
            _singers = [];
        }

        return _singers;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(List<Singer> singers)
    {
        _singers = singers;
        await AtomicFile.WriteAsync(_filePath, stream => JsonSerializer.SerializeAsync(stream, singers, SingerJsonContext.Default.ListSinger));
        _log.LogDebug("Singers saved: {Count} to {Path}", singers.Count, _filePath);
    }
}
