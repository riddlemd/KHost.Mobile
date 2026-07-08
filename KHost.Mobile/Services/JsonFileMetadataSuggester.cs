using System.Text.Json;
using KHost.Mobile.Client.Enrichment;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IMetadataSuggester"/> that wraps an <see cref="ITrackMetadataLookup"/> with a JSON-file
/// cache under the app data directory (same durable-cache pattern as the song store). Keyed by a
/// normalized title+artist; caches misses too so we never re-spend a rate-limited lookup on the same song.
/// </summary>
public sealed class JsonFileMetadataSuggester(ITrackMetadataLookup lookup) : IMetadataSuggester
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "metadata-cache.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Dictionary<string, CacheEntry>? _cache;

    public async Task<TrackMetadata?> SuggestAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var key = CacheKey(title, artist);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await LoadAsync();
            if (cache.TryGetValue(key, out var hit))
                return hit.ToMetadata();
        }
        finally
        {
            _gate.Release();
        }

        // Fetch outside the lock (it's network I/O). A transient failure is swallowed and NOT cached.
        TrackMetadata? result;
        try
        {
            result = await lookup.LookupAsync(title, artist, cancellationToken);
        }
        catch (MetadataLookupException)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await LoadAsync();
            cache[key] = CacheEntry.From(result);   // stores a miss (result == null) as well
            await SaveAsync(cache);
        }
        finally
        {
            _gate.Release();
        }

        return result;
    }

    private static string CacheKey(string title, string artist)
        => $"{title.Trim().ToLowerInvariant()}{artist.Trim().ToLowerInvariant()}";

    // Callers must hold _gate.
    private async Task<Dictionary<string, CacheEntry>> LoadAsync()
    {
        if (_cache is not null)
            return _cache;

        if (!File.Exists(_filePath))
            return _cache = [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _cache = await JsonSerializer.DeserializeAsync<Dictionary<string, CacheEntry>>(stream) ?? [];
        }
        catch (JsonException)
        {
            _cache = [];   // corrupt cache is disposable — start clean rather than crash
        }

        return _cache;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(Dictionary<string, CacheEntry> cache)
    {
        _cache = cache;
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, cache, SerializerOptions);
    }

    // A cache row. Found=false is a remembered "no match".
    private sealed class CacheEntry
    {
        public bool Found { get; set; }
        public int? Year { get; set; }
        public string? Genre { get; set; }
        public string? MatchedTitle { get; set; }
        public string? MatchedArtist { get; set; }

        public static CacheEntry From(TrackMetadata? metadata) => metadata is null
            ? new CacheEntry { Found = false }
            : new CacheEntry
            {
                Found = true,
                Year = metadata.Year,
                Genre = metadata.Genre,
                MatchedTitle = metadata.MatchedTitle,
                MatchedArtist = metadata.MatchedArtist,
            };

        public TrackMetadata? ToMetadata() => Found
            ? new TrackMetadata(MatchedTitle, MatchedArtist, Year, Genre)
            : null;
    }
}
