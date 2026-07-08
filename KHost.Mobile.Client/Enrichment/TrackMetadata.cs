namespace KHost.Mobile.Client.Enrichment;

/// <summary>
/// Metadata looked up for a song by title + artist. Any field may be null when the source didn't
/// carry it. <see cref="MatchedTitle"/>/<see cref="MatchedArtist"/> are what the source actually
/// matched, so the caller can sanity-check the hit before trusting <see cref="Year"/>/<see cref="Genre"/>.
/// </summary>
public sealed record TrackMetadata(string? MatchedTitle, string? MatchedArtist, int? Year, string? Genre);
