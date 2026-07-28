using KHost.Mobile.Abstractions.Services;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Infrastructure.Logic;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// The shared machinery behind every JSON-file store: one gate, one in-memory cache, crash-safe writes, and the
/// corrupt-file quarantine. Subclasses say WHICH file, WHICH serializer context, and — when the in-memory shape
/// differs from the stored one — how to convert between them; everything about how the bytes reach disk lives
/// here once.
/// </summary>
/// <remarks>
/// Derived stores keep their own public API — the shape of <c>IVenueStore</c> and <c>ILyricsCache</c> is nothing
/// alike — and call <see cref="LoadAsync"/> / <see cref="SaveAsync"/> inside their own <see cref="Gate"/>-held
/// blocks, exactly as they did when each owned a private copy of this.
/// <para>Most stores cache the list as-is and want <see cref="JsonFileStore{T}"/>, the one-parameter form.</para>
/// <para>No <c>ConfigureAwait</c>: the callers are UI-thread stores relying on the Blazor sync context.</para>
/// </remarks>
/// <typeparam name="TItem">The element type as persisted — the file is always a JSON array of these.</typeparam>
/// <typeparam name="TCache">The in-memory shape. Usually <c>List&lt;TItem&gt;</c>; a keyed store uses a dictionary.</typeparam>
public abstract class JsonFileStore<TItem, TCache> where TCache : class
{
    private TCache? _cache;
    // The key the cache was loaded under. Always null for a shared store; the active singer for a per-singer one.
    private Guid? _loadedFor;

    /// <param name="log">Already resolved by the subclass, so a bare `new` in the tests still gets NullLogger.</param>
    /// <param name="files">Optional so a test can `new` a store bare; DI supplies the logging implementation.</param>
    protected JsonFileStore(ILogger log, IAtomicFile? files = null)
    {
        Log = log;
        Files = files ?? new AtomicFileWriter();
    }

    /// <summary>The crash-safe writer every store's bytes go through.</summary>
    protected IAtomicFile Files { get; }

    /// <summary>Serializes every mutation, so concurrent UI actions can't interleave a load and a save.</summary>
    protected SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>The subclass's logger — messages here are prefixed with <see cref="Label"/>.</summary>
    protected ILogger Log { get; }

    /// <summary>The source-generated <c>List&lt;TItem&gt;</c> entry on this store's context. Source-generated, not
    /// reflection, because the MAUI heads are trimmed.</summary>
    protected abstract JsonTypeInfo<List<TItem>> TypeInfo { get; }

    /// <summary>What this store calls its contents in a log line ("Venues", "Song list").</summary>
    protected abstract string Label { get; }

    /// <summary>The file backing <paramref name="key"/>. A shared store ignores the argument.</summary>
    protected abstract string PathFor(Guid? key);

    /// <summary>Turn what was read off disk into the in-memory shape. Called on every load, including the
    /// empty-file case (with an empty list).</summary>
    protected abstract TCache Project(List<TItem> items);

    /// <summary>Turn the in-memory shape back into what gets written. The inverse of <see cref="Project"/>.</summary>
    protected abstract List<TItem> Flatten(TCache cache);

    /// <summary>
    /// The key the NEXT load should read. Null (the default) means one shared file; a per-singer store returns the
    /// active singer, and returning a different value than last time is what invalidates the cache.
    /// </summary>
    protected virtual Guid? CurrentKey => null;

    /// <summary>
    /// Hook run once per load, on the deserialized items and <b>before</b> <see cref="Project"/> — so a mutation
    /// here reaches the cache. Return true to have the result written straight back; used for one-time on-disk
    /// migrations. Default does nothing.
    /// </summary>
    protected virtual Task<bool> OnLoadedAsync(List<TItem> items) => Task.FromResult(false);

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
    protected async Task<TCache> LoadAsync()
    {
        var key = CurrentKey;
        if (_cache is not null && _loadedFor == key)
            return _cache;

        // First load, or the key changed out from under the cache → (re)load from that key's file.
        _cache = null;
        _loadedFor = key;
        var path = PathFor(key);

        List<TItem> items;
        if (!File.Exists(path))
        {
            Log.LogDebug("{Label} file not found at {Path}; starting empty", Label, path);
            items = [];
        }
        else
        {
            try
            {
                await using var stream = File.OpenRead(path);
                items = await JsonSerializer.DeserializeAsync(stream, TypeInfo) ?? [];
                Log.LogDebug("{Label} loaded: {Count} from {Path}", Label, items.Count, path);
            }
            catch (JsonException ex)
            {
                // Corrupt file — move the bad bytes aside, then start clean rather than crash the app.
                // Quarantining rather than overwriting is the only route back to the user's data.
                Log.LogWarning(ex, "{Label} file at {Path} is corrupt; quarantining it and starting empty", Label, path);
                if (!Files.Quarantine(path))
                    Log.LogWarning("Corrupt {Path} could not be quarantined; the next save will overwrite it", path);
                items = [];
            }
        }

        var migrated = await OnLoadedAsync(items);
        _cache = Project(items);
        if (migrated)
            await SaveAsync(_cache);

        return _cache;
    }

    /// <summary>Persist <paramref name="cache"/> and make it the cache. <b>Callers must hold <see cref="Gate"/>.</b></summary>
    protected async Task SaveAsync(TCache cache)
    {
        _cache = cache;
        // _loadedFor, NOT CurrentKey: a singer switch landing between the load and this write would otherwise
        // put one singer's data into another singer's file.
        var path = PathFor(_loadedFor);
        var items = Flatten(cache);
        await Files.WriteAsync(path, stream => JsonSerializer.SerializeAsync(stream, items, TypeInfo));
        Log.LogDebug("{Label} saved: {Count} to {Path}", Label, items.Count, path);
    }
}

/// <summary>
/// A <see cref="JsonFileStore{TItem,TCache}"/> whose in-memory shape is the stored list itself — which is every
/// store except the lyrics cache. Derive from this unless you genuinely need a different cache shape.
/// </summary>
/// <typeparam name="T">The element type, both on disk and in memory.</typeparam>
public abstract class JsonFileStore<T>(ILogger log, IAtomicFile? files = null) : JsonFileStore<T, List<T>>(log, files)
{
    /// <inheritdoc />
    protected sealed override List<T> Project(List<T> items) => items;

    /// <inheritdoc />
    protected sealed override List<T> Flatten(List<T> cache) => cache;
}
