using System.Text.Json.Serialization;

namespace KHost.Mobile.Models;

/// <summary>Where a saved song sits in the singer's journey. Starts as a wishlist entry; extends to history later.</summary>
public enum SongListItemStatus
{
    /// <summary>On the "songs I'd like to sing" list.</summary>
    WantToSing,

    /// <summary>Already performed — reserved for the future "sang songs" history integration.</summary>
    Sang,
}

/// <summary>
/// A song the patron has saved on their device. Deliberately local-first: title/artist are free text, not a
/// reference to a server library, so a singer can jot down anything offline. A future sync step can link this to
/// a real library song (<c>LibrarySongId</c>) and flip <see cref="Status"/> to <see cref="SongListItemStatus.Sang"/>.
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

    /// <summary>Release year of the song. Null when unset.</summary>
    public int? Year { get; set; }

    /// <summary>Star rating 1 (shaky) to 5 (nailed it); 0 means unsung / unrated. Defaults to 0. This drives the
    /// sung-state — <see cref="Status"/> is <see cref="SongListItemStatus.Sang"/> exactly when this is 1 or more.</summary>
    public int Confidence { get; set; }

    /// <summary>Starred by the patron. Favorites always float to the top of the list.</summary>
    public bool IsFavorite { get; set; }

    public SongListItemStatus Status { get; set; } = SongListItemStatus.WantToSing;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Running history of every time the song was sung, one timestamp per performance. This is the sung-state
    /// source of truth: <see cref="Status"/> is <see cref="SongListItemStatus.Sang"/> exactly when this list is
    /// non-empty. Empty by default; entries persisted before this field existed deserialize to an empty list.</summary>
    public List<DateTimeOffset> SungDates { get; set; } = [];

    /// <summary>The most recent sung timestamp, or null if never sung. Derived from <see cref="SungDates"/>; not persisted.</summary>
    [JsonIgnore]
    public DateTimeOffset? LastSungAt => SungDates is { Count: > 0 } ? SungDates.Max() : null;

    /// <summary>Future link to a KHost.Online library song once online sync exists. Null for offline-only entries.</summary>
    public Guid? LibrarySongId { get; set; }

    /// <summary>True once we've run the keyless year/genre auto-lookup for this song (hit OR miss), so we never
    /// re-spend a rate-limited call on it. Replaces the old title+artist metadata cache. Defaults to false;
    /// entries persisted before this field existed deserialize to false and get looked up on next open.</summary>
    public bool MetadataLookedUp { get; set; }
}
