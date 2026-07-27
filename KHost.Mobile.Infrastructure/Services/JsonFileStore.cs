using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Infrastructure.Logic;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// The shared machinery behind every JSON-file store: one gate, one in-memory cache, crash-safe writes, and the
/// corrupt-file quarantine. Subclasses say WHICH file and WHICH serializer context; everything about how the
/// bytes reach disk lives here once.
/// </summary>
/// <remarks>
/// Derived stores keep their own public API — the shape of <c>IVenueStore</c> and <c>ISongListStore</c> is
/// nothing alike — and call <see cref="LoadAsync"/> / <see cref="SaveAsync"/> inside their own
/// <see cref="Gate"/>-held blocks, exactly as they did when each owned a private copy of this.
/// <para>No <c>ConfigureAwait</c>: the callers are UI-thread stores relying on the Blazor sync context.</para>
/// </remarks>
/// <typeparam name="T">The element type as persisted — the file is always a JSON array of these.</typeparam>
public abstract class JsonFileStore<T>
{
    private List<T>? _cache;
    // The key the cache was loaded under. Always null for a shared store; the active singer for a per-singer one.
    private Guid? _loadedFor;

    /// <param name="log">Already resolved by the subclass, so a bare `new` in the tests still gets NullLogger.</param>
    protected JsonFileStore(ILogger log) => Log = log;

    /// <summary>Serializes every mutation, so concurrent UI actions can't interleave a load and a save.</summary>
    protected SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>The subclass's logger — messages here are prefixed with <see cref="Label"/>.</summary>
    protected ILogger Log { get; }

    /// <summary>The source-generated <c>List&lt;T&gt;</c> entry on this store's context. Source-generated, not
    /// reflection, because the MAUI heads are trimmed.</summary>
    protected abstract JsonTypeInfo<List<T>> TypeInfo { get; }

    /// <summary>What this store calls its contents in a log line ("Venues", "Song list").</summary>
    protected abstract string Label { get; }

    /// <summary>The file backing <paramref name="key"/>. A shared store ignores the argument.</summary>
    protected abstract string PathFor(Guid? key);

    /// <summary>
    /// The key the NEXT load should read. Null (the default) means one shared file; a per-singer store returns the
    /// active singer, and returning a different value than last time is what invalidates the cache.
    /// </summary>
    protected virtual Guid? CurrentKey => null;

    /// <summary>
    /// Hook run once per load, after deserializing. Return true to have the (mutated) list written straight back —
    /// used for one-time on-disk migrations. Default does nothing.
    /// </summary>
    protected virtual Task<bool> OnLoadedAsync(List<T> items) => Task.FromResult(false);

    /// <inheritdoc cref="IVenueStore.Changed" />
    public event EventHandler? Changed;

    /// <summary>Raise <see cref="Changed"/> — call it AFTER releasing <see cref="Gate"/>, and only when something
    /// actually changed.</summary>
    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Drop the cache so the next load re-reads from disk.</summary>
    protected void InvalidateCache()
    {
        _cache = null;
        _loadedFor = null;
    }

    /// <summary>The current contents, from cache or disk. A corrupt file is quarantined and read as empty.
    /// <b>Callers must hold <see cref="Gate"/>.</b></summary>
    protected async Task<List<T>> LoadAsync()
    {
        var key = CurrentKey;
        if (_cache is not null && _loadedFor == key)
            return _cache;

        // First load, or the key changed out from under the cache → (re)load from that key's file.
        _cache = null;
        _loadedFor = key;
        var path = PathFor(key);

        if (!File.Exists(path))
        {
            Log.LogDebug("{Label} file not found at {Path}; starting empty", Label, path);
            return _cache = [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            _cache = await JsonSerializer.DeserializeAsync(stream, TypeInfo) ?? [];
            Log.LogDebug("{Label} loaded: {Count} from {Path}", Label, _cache.Count, path);
        }
        catch (JsonException ex)
        {
            // Corrupt file — move the bad bytes aside, then start clean rather than crash the app. Quarantining
            // rather than overwriting is the only route back to the user's data.
            Log.LogWarning(ex, "{Label} file at {Path} is corrupt; quarantining it and starting empty", Label, path);
            if (!AtomicFile.Quarantine(path))
                Log.LogWarning("Corrupt {Path} could not be quarantined; the next save will overwrite it", path);
            _cache = [];
        }

        if (await OnLoadedAsync(_cache))
            await SaveAsync(_cache);

        return _cache;
    }

    /// <summary>Persist <paramref name="items"/> and make them the cache. <b>Callers must hold <see cref="Gate"/>.</b></summary>
    protected async Task SaveAsync(List<T> items)
    {
        _cache = items;
        // _loadedFor, NOT CurrentKey: a singer switch landing between the load and this write would otherwise
        // put one singer's data into another singer's file.
        var path = PathFor(_loadedFor);
        await AtomicFile.WriteAsync(path, stream => JsonSerializer.SerializeAsync(stream, items, TypeInfo));
        Log.LogDebug("{Label} saved: {Count} to {Path}", Label, items.Count, path);
    }
}
