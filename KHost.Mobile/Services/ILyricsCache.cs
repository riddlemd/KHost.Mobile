using KHost.Mobile.Client.Lyrics;

namespace KHost.Mobile.Services;

/// <summary>
/// A cached lyrics lookup. A hit whose <see cref="Result"/> is null is a cached "no match" — distinct from
/// <see cref="ILyricsCache.GetAsync"/> returning null, which means nothing is cached for that song at all.
/// </summary>
public sealed record LyricsCacheHit(LyricsResult? Result);

/// <summary>
/// On-device cache of lyrics lookups, keyed by title+artist. Lets the lyrics sheet serve a repeat open without
/// re-hitting LRCLIB. The UI binds to this interface only; a server-backed cache could drop in later. Both a
/// found result and a "no match" are cacheable (see <see cref="LyricsCacheHit"/>). <see cref="Changed"/> fires
/// on any mutation so the Settings page can keep its entry count live.
/// </summary>
public interface ILyricsCache
{
    /// <summary>Look up a cached result. Returns null when nothing is cached for this song.</summary>
    Task<LyricsCacheHit?> GetAsync(string title, string artist);

    /// <summary>Store a lookup result (a null <paramref name="result"/> caches a "no match").</summary>
    Task SetAsync(string title, string artist, LyricsResult? result);

    /// <summary>Drop every cached entry.</summary>
    Task ClearAsync();

    /// <summary>Number of cached entries.</summary>
    Task<int> CountAsync();

    event EventHandler? Changed;
}
