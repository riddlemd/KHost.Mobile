using System.Text.Json;
using KHost.Mobile.Abstractions.Clients.Lyrics;
using KHost.Mobile.Infrastructure.Serialization;
using KHost.Mobile.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.IntegrationTests.Infrastructure.Services;

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
    public async Task A_title_artist_split_does_not_collide_with_a_different_split_of_the_same_text()
    {
        // The key joins title and artist with a separator for exactly this reason — concatenating them plainly
        // would make "AB"/"C" and "A"/"BC" the same entry.
        var cache = NewCache();
        await cache.SetAsync("AB", "C", Lyrics("first song"));
        await cache.SetAsync("A", "BC", Lyrics("different song"));

        Assert.Equal(2, await cache.CountAsync());
        Assert.Equal("first song", (await cache.GetAsync("AB", "C"))!.Result!.PlainLyrics);
        Assert.Equal("different song", (await cache.GetAsync("A", "BC"))!.Result!.PlainLyrics);
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
        // The stored key is title + U+001F + artist, so this is what a GetAsync("dup", "") lookup computes.
        const string Key = "dup\u001f";
        var seeded = new List<LyricsCacheEntry>
        {
            new() { Key = Key, Title = "dup", Artist = "", Found = true, MatchedTitle = "First" },
            new() { Key = Key, Title = "dup", Artist = "", Found = true, MatchedTitle = "Second" },
            new() { Key = "", Title = "orphan" },   // blank key → dropped
        };
        await File.WriteAllTextAsync(
            _dir.FilePath("lyrics-cache.json"),
            JsonSerializer.Serialize(seeded, LyricsCacheJsonContext.Default.ListLyricsCacheEntry));

        var cache = NewCache();

        Assert.Equal(1, await cache.CountAsync());
        var hit = await cache.GetAsync("dup", "");
        Assert.Equal("Second", hit!.Result!.MatchedTitle);   // last wins, not first
    }

    [Fact]
    public async Task Stamps_CachedAt_in_local_time_not_UTC()
    {
        // A zone whose offset is non-zero at the instant under test, so local and UTC are distinguishable.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(-5), "t", "t"));
        var cache = new JsonFileLyricsCache(_dir, timeProvider: clock);

        await cache.SetAsync("Song", "Artist", Lyrics("words"));

        // Read the raw file: CachedAt is a housekeeping stamp the ILyricsCache surface doesn't expose.
        var stored = JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(_dir.FilePath("lyrics-cache.json")),
            LyricsCacheJsonContext.Default.ListLyricsCacheEntry)!;

        var entry = Assert.Single(stored);
        Assert.Equal(TimeSpan.FromHours(-5), entry.CachedAt.Offset);
        Assert.Equal(clock.GetUtcNow(), entry.CachedAt.ToUniversalTime());
    }

    [Fact]
    public async Task A_corrupt_file_loads_as_an_empty_cache_and_is_quarantined_to_a_dot_corrupt_sibling()
    {
        var path = _dir.FilePath("lyrics-cache.json");
        await File.WriteAllTextAsync(path, "not json at all}");   // e.g. a pre-atomic-write interrupted save

        Assert.Equal(0, await NewCache().CountAsync());

        Assert.False(File.Exists(path));                         // the bad file was moved aside...
        Assert.True(File.Exists(path + ".corrupt"));              // ...to a .corrupt sibling...
        Assert.Equal("not json at all}", await File.ReadAllTextAsync(path + ".corrupt"));   // ...with its bytes intact
    }
}
