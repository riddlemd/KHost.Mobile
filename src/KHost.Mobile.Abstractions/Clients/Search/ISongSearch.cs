namespace KHost.Mobile.Abstractions.Clients.Search;

/// <summary>
/// Search-as-you-type over an online catalogue, for the Add-a-song title field: finds songs from a partial,
/// misspelled or half-remembered title. Keyless. Tolerant by design, unlike <c>ITrackMetadataLookup</c>, which
/// matches an exact song for enrichment; this one offers candidates for a person to choose from.
/// </summary>
public interface ISongSearch
{
    /// <summary>
    /// Up to <paramref name="limit"/> distinct songs matching <paramref name="title"/> (and, when given,
    /// <paramref name="artist"/>), best first. Versions of one song (remasters, live takes, re-recordings) come
    /// back as ONE hit with <see cref="SongSearchHit.OtherVersions"/> counting the rest; covers and karaoke /
    /// tribute tracks are left out.
    /// <para>Returns an empty list when nothing matched or the query is too short, and <c>null</c> when the
    /// catalogue couldn't be reached (offline, rate-limited, unusable reply). The caller tells "no results"
    /// from "no search" by that. Never throws, except for the caller's own cancellation.</para>
    /// </summary>
    Task<IReadOnlyList<SongSearchHit>?> SearchAsync(
        string title, string? artist, int limit = 6, CancellationToken cancellationToken = default);
}

/// <summary>One song offered by <see cref="ISongSearch"/>, already tidied for display.</summary>
/// <param name="Title">The title with version asides ("(2011 Remaster)", "(feat. X)") removed.</param>
/// <param name="Artist">The artist, with any featured artist from the title appended ("Mark Ronson ft. Amy Winehouse").</param>
/// <param name="Album">The album of the version shown, or null.</param>
/// <param name="ArtworkUrl">That album's cover at full size, to keep on the song once it's added; or null.</param>
/// <param name="ThumbnailUrl">The same cover small enough for a list row; or null.</param>
/// <param name="OtherVersions">How many other versions of the same song were folded into this hit.</param>
public sealed record SongSearchHit(
    string Title, string Artist, string? Album, string? ArtworkUrl, string? ThumbnailUrl, int OtherVersions);
