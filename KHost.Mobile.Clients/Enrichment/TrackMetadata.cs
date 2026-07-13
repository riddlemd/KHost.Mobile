namespace KHost.Mobile.Clients.Enrichment;

/// <summary>
/// Metadata looked up for a song by title + artist. Any field may be null when the source didn't
/// carry it. <see cref="MatchedTitle"/>/<see cref="MatchedArtist"/> are what the source actually
/// matched, so the caller can sanity-check the hit before trusting <see cref="Year"/>/<see cref="Genre"/>.
/// </summary>
/// <param name="ArtworkUrl">Absolute URL of the cover image (from the same matched result), or null when the
/// source carried none. Not downloaded here — the caller decides whether to fetch/cache the bytes.</param>
public sealed record TrackMetadata(string? MatchedTitle, string? MatchedArtist, int? Year, string? Genre, string? ArtworkUrl);
