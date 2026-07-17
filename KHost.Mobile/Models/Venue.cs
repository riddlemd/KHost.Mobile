using System.Text.Json.Serialization;

namespace KHost.Mobile.Models;

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

    /// <summary>An assignable emoji shown on venue rows / the header chip for at-a-glance ID. Defaults to the mic;
    /// the add/edit form picks from the curated <see cref="VenueGlyphs"/> set. Stored as the raw emoji string.</summary>
    public string Glyph { get; set; } = VenueGlyphs.Default;

    /// <summary>The venue's KaraFun catalog ID, or null if it doesn't use KaraFun. When set, powers "Open KaraFun
    /// Catalog". Digits with leading zeros significant — stored as a string exactly as it appears in the link.</summary>
    public string? KaraFunVenueId { get; set; }

    /// <summary>Latitude captured via "use my current location", or null if not set. Paired with
    /// <see cref="Longitude"/>; enables nearest-venue auto-select once geolocation lands.</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitude captured via "use my current location", or null if not set. See <see cref="Latitude"/>.</summary>
    public double? Longitude { get; set; }

    /// <summary>Starred by the singer. Favorites float to the top of the venue list and the switcher.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>When false, the venue is kept out of the header switcher's quick list — it still exists on the Venues
    /// page, can be set active there, and still tags sings / opens its KaraFun catalog. Defaults to <c>true</c> so
    /// existing venues (and a file that predates this field) stay listed.</summary>
    public bool ShowInSwitcher { get; set; } = true;

    /// <summary>Free-text notes ("great sound", "cash only", "ask for Dana"). Null when unset.</summary>
    public string? Notes { get; set; }

    /// <summary>When the venue was added.</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>True when both <see cref="Latitude"/> and <see cref="Longitude"/> are set. Derived; not persisted.</summary>
    [JsonIgnore]
    public bool HasLocation => Latitude is not null && Longitude is not null;
}
