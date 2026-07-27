namespace KHost.Mobile.Models;

/// <summary>
/// A portable export of one singer — their identity plus their whole song list <em>including sung history</em> —
/// so a person can be moved to another device or backed up. Deliberately does <b>not</b> carry the Tonight set
/// (that's an on-the-night, device-local thing) nor venues (those are shared across singers and travel in their
/// own export). Ids are preserved on both the singer and the songs, so re-importing a profile updates the same
/// singer rather than duplicating them (a true backup → restore).
/// A <see cref="Version"/> + <see cref="App"/> marker make the file self-identifying for import detection.
/// </summary>
public sealed record SingerProfile(
    int Version,
    string App,
    Singer Singer,
    List<SongListItem> Songs,
    DateTimeOffset ExportedAt)
{
    /// <summary>The current profile format version. Bump when the shape changes incompatibly.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Marker written to <see cref="App"/> so an imported file can be recognised as a KHost Cue profile.</summary>
    public const string Marker = "khost-cue-profile";

    /// <summary>Build a profile for the given singer and songs, stamped now at the current version.</summary>
    public static SingerProfile Create(Singer singer, IEnumerable<SongListItem> songs) =>
        new(CurrentVersion, Marker, singer, [.. songs], DateTimeOffset.Now);
}
