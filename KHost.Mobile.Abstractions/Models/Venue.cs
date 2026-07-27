using System.Text.Json.Serialization;

namespace KHost.Mobile.Abstractions.Models;

/// <summary>
/// A karaoke venue the singer keeps on their device — a local, user-authored record. Identity is a local
/// <see cref="Id"/> GUID, <em>not</em> the KaraFun ID: a venue may have no KaraFun catalog (or a non-KaraFun one),
/// and the KaraFun id carries no name/address/coordinates (there is no KaraFun venue directory), so all identity
/// here is local. Mutable class per the persisted-entity convention; every field beyond <see cref="Name"/> is
/// optional/defaulted so adding more later stays migration-free (mirrors <see cref="SongListItem"/>).
/// </summary>
public sealed class Venue
{
    /// <summary>Stable local identity. Not the KaraFun ID (see <see cref="KaraFunVenueId"/>).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The only required field — the human label ("The Mint", "Palms Thursday karaoke").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An assignable emoji identifying the venue, from the curated <see cref="VenueGlyphs"/> set.
    /// Defaults to the mic. Raw emoji string.</summary>
    public string Glyph { get; set; } = VenueGlyphs.Default;

    /// <summary>The venue's KaraFun catalog ID, or null if it doesn't use KaraFun. Leading zeros are significant —
    /// stored as a string exactly as it appears in the link.</summary>
    public string? KaraFunVenueId { get; set; }

    /// <summary>Latitude captured via "use my current location", or null if not set. Paired with
    /// <see cref="Longitude"/>; both are required for nearest-venue auto-select.</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitude captured via "use my current location", or null if not set. See <see cref="Latitude"/>.</summary>
    public double? Longitude { get; set; }

    /// <summary>Starred by the singer. Favorites float to the top of the venue list and the switcher.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>When false, the venue is kept out of the quick switcher — it still exists, can be set active, and
    /// still tags sings. Defaults to <c>true</c> so a file that predates this field stays listed.</summary>
    public bool ShowInSwitcher { get; set; } = true;

    /// <summary>Free-text notes ("great sound", "cash only", "ask for Dana"). Null when unset.</summary>
    public string? Notes { get; set; }

    /// <summary>When the venue was added.</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>True when both <see cref="Latitude"/> and <see cref="Longitude"/> are set. Derived; not persisted.</summary>
    [JsonIgnore]
    public bool HasLocation => Latitude is not null && Longitude is not null;
}
