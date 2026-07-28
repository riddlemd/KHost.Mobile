using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>Reads and writes the JSON a singer profile or venue list travels as.</summary>
public interface ISingerProfileCodec
{
    /// <summary>What an imported file turned out to be.</summary>
    ProfileFileKind Detect(string json);

    string Serialize(SingerProfile profile);
    string SerializeVenues(IReadOnlyList<Venue> venues);

    /// <summary>Null when the JSON isn't a profile.</summary>
    SingerProfile? ParseProfile(string json);

    /// <summary>Null when the JSON isn't a bare song list (an older export).</summary>
    List<SongListItem>? ParseLegacySongs(string json);

    /// <summary>Null when the JSON isn't a venue list.</summary>
    List<Venue>? ParseVenues(string json);
}
