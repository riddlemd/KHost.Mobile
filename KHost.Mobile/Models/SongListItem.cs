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

    /// <summary>Star rating 1 (shaky) to 5 (nailed it); 0 means unsung / unrated. Defaults to 0. This drives the
    /// sung-state — <see cref="Status"/> is <see cref="SongListItemStatus.Sang"/> exactly when this is 1 or more.</summary>
    public int Confidence { get; set; }

    /// <summary>Starred by the patron. Favorites always float to the top of the list.</summary>
    public bool IsFavorite { get; set; }

    public SongListItemStatus Status { get; set; } = SongListItemStatus.WantToSing;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Set when the song moves to <see cref="SongListItemStatus.Sang"/>. Null while still a wishlist entry.</summary>
    public DateTimeOffset? SungAt { get; set; }

    /// <summary>Future link to a KHost.Online library song once online sync exists. Null for offline-only entries.</summary>
    public Guid? LibrarySongId { get; set; }
}
