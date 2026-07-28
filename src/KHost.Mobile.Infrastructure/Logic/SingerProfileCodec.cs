using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using KHost.Mobile.Infrastructure.Serialization;
using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// Pure (no-I/O) serialize/parse for the Import/Export page's file round-trips — a singer profile, a legacy
/// songs-only export, or a venue list. Detection lets the profile import accept both a new profile and an old
/// songs-only file (a bare JSON array) and route each correctly.
/// </summary>
internal sealed class SingerProfileCodec(ILogger<SingerProfileCodec>? logger = null) : ISingerProfileCodec
{
    // Held for future diagnostics: the seam should exist before it's needed, not be retrofitted.
    private readonly ILogger _log = logger ?? NullLogger<SingerProfileCodec>.Instance;

    /// <summary>What a picked file for the <em>profile</em> import turned out to be.</summary>
    public enum FileKind
    {
        /// <summary>A <see cref="SingerProfile"/> object (identity + songs + history).</summary>
        Profile,
        /// <summary>A legacy songs-only export — a bare JSON array of <see cref="SongListItem"/>.</summary>
        LegacySongList,
        /// <summary>Not JSON, or not a shape we recognise.</summary>
        Invalid,
    }

    /// <summary>Serialize a profile for export.</summary>
    public string Serialize(SingerProfile profile) =>
        JsonSerializer.Serialize(profile, ProfileJsonContext.Default.SingerProfile);

    /// <summary>Serialize a venue list for the separate venues export.</summary>
    public string SerializeVenues(IReadOnlyList<Venue> venues) =>
        JsonSerializer.Serialize([.. venues], VenueJsonContext.Default.ListVenue);

    /// <summary>
    /// Classify a profile-import file by its JSON root: an object carrying a <c>Singer</c> is a profile; a bare
    /// array is a legacy songs-only export; anything else (or invalid JSON) is <see cref="ProfileFileKind.Invalid"/>.
    /// </summary>
    public ProfileFileKind Detect(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => ProfileFileKind.LegacySongList,
                JsonValueKind.Object when doc.RootElement.TryGetProperty("Singer", out _) => ProfileFileKind.Profile,
                _ => ProfileFileKind.Invalid,
            };
        }
        catch (JsonException)
        {
            return ProfileFileKind.Invalid;
        }
    }

    /// <summary>Parse a profile file, or null if it isn't valid.</summary>
    public SingerProfile? ParseProfile(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, ProfileJsonContext.Default.SingerProfile);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parse a legacy songs-only export (a bare array), or null if it isn't valid.</summary>
    public List<SongListItem>? ParseLegacySongs(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, SongListJsonContext.Default.ListSongListItem);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parse a venues export (a bare array), or null if it isn't valid.</summary>
    public List<Venue>? ParseVenues(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, VenueJsonContext.Default.ListVenue);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
