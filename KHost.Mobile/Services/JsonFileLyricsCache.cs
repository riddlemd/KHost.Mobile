using System.Text.Json;
using KHost.Mobile.Client.Lyrics;
using KHost.Mobile.Models;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ILyricsCache"/> backed by a single JSON file in the app's private data directory — the same
/// durable-JSON pattern as <see cref="JsonFileSongListStore"/>. The in-memory dictionary is the source of truth
/// once loaded; every mutation rewrites the file. A <see cref="SemaphoreSlim"/> guards both against concurrent
/// UI actions. A corrupt file is treated as an empty cache rather than crashing the app.
/// </summary>
public sealed class JsonFileLyricsCache : ILyricsCache
{
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "lyrics-cache.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Dictionary<string, LyricsCacheEntry>? _entries;

    public event EventHandler? Changed;

    public async Task<LyricsCacheHit?> GetAsync(string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var key = KeyFor(title, artist);
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            return entries.TryGetValue(key, out var entry) ? new LyricsCacheHit(ToResult(entry)) : null;
        }
        finally
        {
            _gate.Release();
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
            CachedAt = DateTimeOffset.Now,
        };

        await _gate.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            entries[entry.Key] = entry;   // upsert: a re-lookup refreshes the cached answer
            await SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await _gate.WaitAsync();
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
            _gate.Release();
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> CountAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return (await LoadAsync()).Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Trimmed, case-insensitive title + artist joined by the ASCII unit separator (U+001F). That separator keeps
    // "AB"+"C" distinct from "A"+"BC", and never appears in a real title/artist. Mirrors the song-list dedupe key.
    private static string KeyFor(string title, string artist)
        => $"{title.Trim().ToLowerInvariant()}{artist.Trim().ToLowerInvariant()}";

    private static LyricsResult? ToResult(LyricsCacheEntry e)
        => e.Found
            ? new LyricsResult(e.MatchedTitle, e.MatchedArtist, e.PlainLyrics, e.SyncedLyrics, e.Instrumental)
            : null;

    // Callers must hold _gate.
    private async Task<Dictionary<string, LyricsCacheEntry>> LoadAsync()
    {
        if (_entries is not null)
            return _entries;

        if (!File.Exists(_filePath))
            return _entries = new Dictionary<string, LyricsCacheEntry>(StringComparer.Ordinal);

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync(stream, LyricsCacheJsonContext.Default.ListLyricsCacheEntry) ?? [];
            // Last-write-wins on a duplicate key (shouldn't happen, but a hand-edited file could carry one).
            _entries = list
                .Where(e => !string.IsNullOrEmpty(e.Key))
                .GroupBy(e => e.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Corrupt file (e.g. an interrupted write) — start clean rather than crash the app.
            _entries = new Dictionary<string, LyricsCacheEntry>(StringComparer.Ordinal);
        }

        return _entries;
    }

    // Callers must hold _gate.
    private async Task SaveAsync(Dictionary<string, LyricsCacheEntry> entries)
    {
        _entries = entries;
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, entries.Values.ToList(), LyricsCacheJsonContext.Default.ListLyricsCacheEntry);
    }
}
