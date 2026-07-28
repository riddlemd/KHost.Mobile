using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Abstractions.Clients.Lyrics;
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
/// <see cref="JsonFileSongListStore"/>. A corrupt file is quarantined and treated as an empty cache.
/// </remarks>
internal sealed class JsonFileLyricsCache
    : JsonFileStore<LyricsCacheEntry, Dictionary<string, LyricsCacheEntry>>, ILyricsCache
{
    private readonly string _filePath;
    // GetLocalNow, not GetUtcNow: every stored timestamp in this app is local, and switching would shift
    // every new entry by the UTC offset against the ones already on the device.
    private readonly TimeProvider _clock;

    // logger is optional so the integration tests can `new` the cache without a logging stack; DI supplies the real one.
    public JsonFileLyricsCache(
        IAppDataDirectory paths,
        ILogger<JsonFileLyricsCache>? logger = null,
        TimeProvider? timeProvider = null,
        IAtomicFile? files = null)
        : base(logger ?? NullLogger<JsonFileLyricsCache>.Instance, files)
    {
        _filePath = Path.Combine(paths.AppDataDirectory, "lyrics-cache.json");
        _clock = timeProvider ?? TimeProvider.System;
    }

    protected override JsonTypeInfo<List<LyricsCacheEntry>> TypeInfo => LyricsCacheJsonContext.Default.ListLyricsCacheEntry;
    protected override string Label => "Lyrics cache";
    protected override string PathFor(Guid? key) => _filePath;

    // Keyed in memory, a flat array on disk. Blank keys are dropped and a duplicate key is last-write-wins —
    // neither should happen, but a hand-edited file could carry either.
    protected override Dictionary<string, LyricsCacheEntry> Project(List<LyricsCacheEntry> items) =>
        items
            .Where(e => !string.IsNullOrEmpty(e.Key))
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    protected override List<LyricsCacheEntry> Flatten(Dictionary<string, LyricsCacheEntry> cache) =>
        cache.Values.ToList();

    public async Task<LyricsCacheHit?> GetAsync(string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var key = KeyFor(title, artist);
        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            return entries.TryGetValue(key, out var entry) ? new LyricsCacheHit(ToResult(entry)) : null;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SetAsync(string title, string artist, LyricsResult? result)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var entry = new LyricsCacheEntry
        {
            Key = KeyFor(title, artist),
            Title = title,
            Artist = artist,
            Found = result is not null,
            MatchedTitle = result?.MatchedTitle,
            MatchedArtist = result?.MatchedArtist,
            PlainLyrics = result?.PlainLyrics,
            SyncedLyrics = result?.SyncedLyrics,
            Instrumental = result?.Instrumental ?? false,
            CachedAt = _clock.GetLocalNow(),
        };

        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            entries[entry.Key] = entry;   // upsert: a re-lookup refreshes the cached answer
            await SaveAsync(entries);
        }
        finally
        {
            Gate.Release();
        }

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

    public async Task<int> CountAsync()
    {
        await Gate.WaitAsync();
        try
        {
            return (await LoadAsync()).Count;
        }
        finally
        {
            Gate.Release();
        }
    }

    // Trimmed, case-insensitive title + artist joined by the ASCII unit separator (U+001F): it keeps "AB"+"C"
    // distinct from "A"+"BC" and never appears in a real title/artist.
    private static string KeyFor(string title, string artist)
        => $"{title.Trim().ToLowerInvariant()}{artist.Trim().ToLowerInvariant()}";

    private static LyricsResult? ToResult(LyricsCacheEntry e)
        => e.Found
            ? new LyricsResult(e.MatchedTitle, e.MatchedArtist, e.PlainLyrics, e.SyncedLyrics, e.Instrumental)
            : null;
}
