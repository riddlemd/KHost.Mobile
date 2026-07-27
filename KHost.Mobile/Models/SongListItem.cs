using System.Text.Json.Serialization;

namespace KHost.Mobile.Models;

/// <summary>Where a saved song sits in the singer's journey. Starts as a wishlist entry; extends to history later.</summary>
public enum SongListItemStatus
{
    /// <summary>On the "songs I'd like to sing" list.</summary>
    WantToSing,

    /// <summary>Performed at least once — set automatically whenever the song's <c>Performances</c> list is non-empty.</summary>
    Sang,
}

/// <summary>
/// One time the song was performed. Each sing carries its own "how it went" rating, so the song's displayed rating
/// is the average across performances rather than a single overwrite. Mutable class (not a record) because a rating
/// can be edited after the fact from the history sheet.
/// </summary>
public sealed class Performance
{
    /// <summary>Stable identity for this performance, so other stores (e.g. the Tonight set) can reference the exact
    /// one they logged and undo it later. Entries persisted before this field existed deserialize to a new id on
    /// load — harmless, nothing references those.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>When the song was sung.</summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>"How it went" for this specific sing: 1 (shaky) to 5 (nailed it). 0 means logged-but-not-yet-rated
    /// (the singer skipped the rating, or it came from a pre-per-performance migration) and is excluded from the average.</summary>
    public int HowItWent { get; set; }

    /// <summary>An optional note about this specific performance (e.g. "crowd loved it", "forgot the bridge"). Null when unset.</summary>
    public string? Note { get; set; }

    /// <summary>The <see cref="Venue.Id"/> the singer was at when this sing was logged, or null when there was no
    /// active venue (or the performance predates venues). Stamped from the session's active venue at log time and
    /// never changed after; powers per-venue history. A venue deleted later leaves this as an orphan id — harmless,
    /// it just no longer resolves to a saved venue.</summary>
    public Guid? VenueId { get; set; }
}

/// <summary>
/// A song the patron has saved on their device. Deliberately local-first: title/artist are free text, not a
/// reference to any catalogue, so a singer can jot down anything offline.
/// Mutable class (not a record) because it's an editable, JSON-persisted entity.
/// </summary>
public sealed class SongListItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>Music genre (from <see cref="Genres.All"/> or free text). Null when unset.</summary>
    public string? Genre { get; set; }

    /// <summary>Free-form personal tags the singer attaches to a song ("closer", "duet", "needs practice").
    /// Distinct from <see cref="Genre"/> (a single fixed-list value describing the music); tags are many,
    /// arbitrary and cross-cutting. Normalized on write via <see cref="SongTags.Normalize"/>.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Release year of the song. Null when unset.</summary>
    public int? Year { get; set; }

    /// <summary>How much the patron liked <em>singing</em> this song, apart from how well any given performance went:
    /// 1-5, 0 = not yet rated. Per song (not per performance). Defaults to 0; older entries deserialize to 0.</summary>
    public int Enjoyment { get; set; }

    /// <summary>Starred by the patron. Favorites always float to the top of the list.</summary>
    public bool IsFavorite { get; set; }

    public SongListItemStatus Status { get; set; } = SongListItemStatus.WantToSing;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Every time the song was sung, each with its own "how it went" rating. This is the sung-state source of
    /// truth: <see cref="Status"/> is <see cref="SongListItemStatus.Sang"/> exactly when this list is non-empty. Empty
    /// by default. Files written before per-performance ratings existed are migrated into this list on load (see
    /// <see cref="SungDates"/>/<see cref="Confidence"/>).</summary>
    public List<Performance> Performances { get; set; } = [];

    /// <summary>The song's "how it went" star rating: the average of the rated performances (<see cref="Performance.HowItWent"/>
    /// of 1 or more), or null when it has no rated performances. Derived; not persisted. Round for whole-star display.</summary>
    [JsonIgnore]
    public double? AverageHowItWent
    {
        get
        {
            var rated = Performances.Where(p => p.HowItWent >= 1).Select(p => p.HowItWent).ToList();
            return rated.Count > 0 ? rated.Average() : null;
        }
    }

    /// <summary>The most recent sung timestamp, or null if never sung. Derived from <see cref="Performances"/>; not persisted.</summary>
    [JsonIgnore]
    public DateTimeOffset? LastSungAt => Performances.Count > 0 ? Performances.Max(p => p.Date) : null;

    /// <summary>LEGACY (read/migrate only). Pre-per-performance list of sung timestamps. New writes leave this empty;
    /// the store migrates any values into <see cref="Performances"/> on load. Kept only so old files still deserialize.</summary>
    public List<DateTimeOffset> SungDates { get; set; } = [];

    /// <summary>LEGACY (read/migrate only). The old single "how it went" rating (0-5). Folded into each migrated
    /// <see cref="Performance.HowItWent"/> on load, then left at 0. Kept only so old files still deserialize.</summary>
    public int Confidence { get; set; }

    /// <summary>Reserved external-catalogue id — always null today. Persisted, so keep the property for schema stability.</summary>
    public Guid? LibrarySongId { get; set; }

    /// <summary>True once the keyless year/genre auto-lookup has run for this song (hit OR miss), so a
    /// rate-limited call is never re-spent on it. Entries persisted before this field existed deserialize to
    /// false and get looked up on next open.</summary>
    public bool MetadataLookedUp { get; set; }

    /// <summary>Absolute URL of the song's cover art. Null when unknown or the song had no artwork match. Only
    /// where to fetch the image — the bytes are downloaded and cached separately (see IAlbumArtCache).</summary>
    public string? ArtworkUrl { get; set; }

    /// <summary>True once the artwork lookup has run for this song (hit OR miss), so a rate-limited call is never
    /// re-spent chasing a cover that isn't there. Separate from <see cref="MetadataLookedUp"/> because a song
    /// enriched before album art existed has year/genre but no <see cref="ArtworkUrl"/>.</summary>
    public bool ArtworkLookedUp { get; set; }

    /// <summary>The catalogue's spelling of this song when the lookup found no exact match but one near-miss
    /// that reads like a typo — "Bohemian Rhapsody" for a song entered as "Bohemian Rapsody". Null when the
    /// lookup matched, found nothing close, or the user waved the suggestion off.
    /// <para>Persisted because the lookup behind it is rate-limited and runs once per song: losing this on
    /// restart would mean never showing the hint again.</para></summary>
    public string? SuggestedTitle { get; set; }

    /// <inheritdoc cref="SuggestedTitle" />
    public string? SuggestedArtist { get; set; }

    /// <summary>Whether a spelling correction is on offer for this song.</summary>
    [JsonIgnore]
    public bool HasSuggestion => SuggestedTitle is not null && SuggestedArtist is not null;
}
