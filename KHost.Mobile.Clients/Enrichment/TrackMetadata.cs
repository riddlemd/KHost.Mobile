namespace KHost.Mobile.Clients.Enrichment;

/// <summary>Metadata looked up for a song by title + artist. Any field may be null when the source didn't carry it.</summary>
/// <param name="MatchedTitle">What the source actually matched — sanity-check it against the query before trusting the rest.</param>
/// <param name="MatchedArtist">What the source actually matched — sanity-check it against the query before trusting the rest.</param>
/// <param name="Year">Release year, or null.</param>
/// <param name="Genre">Genre as the source reported it, or null.</param>
/// <param name="ArtworkUrl">Absolute cover-image URL from the same matched result, or null. Not downloaded here —
/// the caller decides whether to fetch/cache the bytes.</param>
public sealed record TrackMetadata(string? MatchedTitle, string? MatchedArtist, int? Year, string? Genre, string? ArtworkUrl);
