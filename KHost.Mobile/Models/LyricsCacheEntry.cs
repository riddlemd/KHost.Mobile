namespace KHost.Mobile.Models;

/// <summary>
/// One cached lyrics lookup, persisted on-device so re-opening a song's lyrics doesn't re-hit LRCLIB. Keyed by
/// the normalized title+artist that was searched. A miss is cached too (<see cref="Found"/> = false) so a known
/// "no lyrics" answer is also served without a network round-trip. Mutable class per the persisted-entity
/// convention (DTOs are records; things we store and rewrite are classes).
/// </summary>
public sealed class LyricsCacheEntry
{
    /// <summary>Normalized lookup key (trimmed, lower-cased title + a separator + artist). Also the map key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The original (untrimmed) query, kept for reference/debugging only.</summary>
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;

    /// <summary>False when the lookup returned no match — a cached negative result.</summary>
    public bool Found { get; set; }

    public string? MatchedTitle { get; set; }
    public string? MatchedArtist { get; set; }
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
    public bool Instrumental { get; set; }

    public DateTimeOffset CachedAt { get; set; }
}
