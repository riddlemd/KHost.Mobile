using System.Text.Json;
using KHost.Mobile.Client.Lyrics;
using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public sealed class JsonFileLyricsCacheTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    private JsonFileLyricsCache NewCache() => new(_dir);

    public void Dispose() => _dir.Dispose();

    private static LyricsResult Lyrics(string plain) => new("Title", "Artist", plain, null, Instrumental: false);

    [Fact]
    public async Task SetAsync_then_GetAsync_returns_the_cached_result_ignoring_case_and_whitespace()
    {
        var cache = NewCache();
        await cache.SetAsync("Bohemian Rhapsody", "Queen", Lyrics("Is this the real life?"));

        var hit = await cache.GetAsync("  BOHEMIAN rhapsody ", "queen");   // different case + padding, same song

        Assert.NotNull(hit);
        Assert.NotNull(hit!.Result);
        Assert.Equal("Is this the real life?", hit.Result!.PlainLyrics);
    }

    [Fact]
    public async Task A_negative_result_is_cached_and_distinct_from_a_miss()
    {
        var cache = NewCache();
        await cache.SetAsync("Instrumental Track", "Composer", result: null);   // cache a "no match"

        var cachedNoMatch = await cache.GetAsync("Instrumental Track", "Composer");
        Assert.NotNull(cachedNoMatch);          // the song IS in the cache...
        Assert.Null(cachedNoMatch!.Result);     // ...as a known "no lyrics"

        Assert.Null(await cache.GetAsync("Never Looked Up", "Nobody"));   // genuinely uncached → null hit
    }

    [Fact]
    public async Task Blank_titles_are_ignored_on_both_read_and_write()
    {
        var cache = NewCache();

        await cache.SetAsync("", "Artist", Lyrics("ignored"));
        Assert.Equal(0, await cache.CountAsync());
        Assert.Null(await cache.GetAsync("   ", "Artist"));
    }

    [Fact]
    public async Task SetAsync_upserts_the_same_key()
    {
        var cache = NewCache();
        await cache.SetAsync("Song", "Artist", Lyrics("first"));
        await cache.SetAsync("Song", "Artist", Lyrics("second"));

        Assert.Equal(1, await cache.CountAsync());
        var hit = await cache.GetAsync("Song", "Artist");
        Assert.Equal("second", hit!.Result!.PlainLyrics);
    }

    [Fact]
    public async Task ClearAsync_empties_the_cache_and_no_ops_when_already_empty()
    {
        var cache = NewCache();
        await cache.SetAsync("Song", "Artist", Lyrics("words"));
        Assert.Equal(1, await cache.CountAsync());
        var fired = 0;
        cache.Changed += (_, _) => fired++;

        await cache.ClearAsync();
        Assert.Equal(0, await cache.CountAsync());
        Assert.Equal(1, fired);

        await cache.ClearAsync();   // already empty → no-op
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task State_persists_to_disk_and_is_read_back_by_a_fresh_instance()
    {
        var writer = NewCache();
        await writer.SetAsync("Song", "Artist", Lyrics("persisted words"));

        var reader = NewCache();
        var hit = await reader.GetAsync("Song", "Artist");
        Assert.Equal("persisted words", hit!.Result!.PlainLyrics);
    }

    [Fact]
    public async Task Loading_collapses_duplicate_keys_and_drops_blank_keyed_entries()
    {
        var seeded = new List<LyricsCacheEntry>
        {
            new() { Key = "dup", Title = "A", Found = true, MatchedTitle = "First" },
            new() { Key = "dup", Title = "A", Found = true, MatchedTitle = "Second" },   // same key, last wins
            new() { Key = "", Title = "orphan" },                                          // blank key → dropped
        };
        await File.WriteAllTextAsync(
            _dir.FilePath("lyrics-cache.json"),
            JsonSerializer.Serialize(seeded, LyricsCacheJsonContext.Default.ListLyricsCacheEntry));

        Assert.Equal(1, await NewCache().CountAsync());
    }

    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_cache_rather_than_throwing()
    {
        await File.WriteAllTextAsync(_dir.FilePath("lyrics-cache.json"), "not json at all}");

        Assert.Equal(0, await NewCache().CountAsync());
    }
}
