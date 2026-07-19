using System.Text.Json.Serialization;

namespace KHost.Mobile.Models;

/// <summary>
/// One singer who shares the device — the owner of a personal My List and Tonight set. Multiple singers can be
/// kept on a single device (a phone passed around the karaoke table); switching the active singer swaps which
/// personal data the app shows, while the Venues list stays shared across everyone. Identity is a local
/// <see cref="Id"/> GUID; that id also namespaces the singer's on-device song-list / tonight files (see
/// <c>SingerDataFiles</c>). Mutable class per the persisted-entity convention; every field beyond <see cref="Name"/>
/// is optional/defaulted so adding more later stays migration-free (mirrors <see cref="Venue"/>).
/// </summary>
public sealed class Singer
{
    /// <summary>Stable local identity. Also namespaces this singer's song-list / tonight data files.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The only meaningful field — the singer's display name ("Mike", "Sam"). Its first letter is the avatar.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The singer's accent color (hex). The add/edit picker chooses from <see cref="SingerColors"/>; the
    /// active singer's color tints the app chrome. Defaults to the brand violet.</summary>
    public string Color { get; set; } = SingerColors.Default;

    /// <summary>An optional emoji shown on the avatar instead of the name's first letter. Null (the default) means
    /// "use the first letter"; the add/edit picker offers a curated set (<see cref="SingerGlyphs"/>) plus a "use the
    /// letter" option that clears it. Stored as the raw emoji string.</summary>
    public string? Glyph { get; set; }

    /// <summary>Display order in the roster and the header switcher. Assigned on add (append to the end); a lower
    /// number sorts first.</summary>
    public int Order { get; set; }

    /// <summary>When the singer was added.</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>The uppercase first letter of <see cref="Name"/> for the avatar, or "?" when the name is blank.
    /// Derived; not persisted.</summary>
    [JsonIgnore]
    public string Initial
    {
        get
        {
            var trimmed = Name.TrimStart();
            return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
        }
    }

    /// <summary>What the avatar shows: the chosen <see cref="Glyph"/> emoji when set, otherwise the name's
    /// <see cref="Initial"/>. Derived; not persisted.</summary>
    [JsonIgnore]
    public string Avatar => string.IsNullOrWhiteSpace(Glyph) ? Initial : Glyph;
}
