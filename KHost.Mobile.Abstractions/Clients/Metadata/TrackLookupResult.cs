using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Metadata;

/// <summary>
/// The outcome of a metadata lookup: either a verified match, or a near-miss worth offering as a correction —
/// never both, and often neither.
/// </summary>
/// <param name="Match">Metadata from a result whose title and artist both matched. Null when none did.</param>
/// <param name="Suggestion">
/// Set only when nothing matched but one result was a near-miss, which usually means a typo. Deliberately
/// carries no year/genre/artwork: it hasn't been confirmed as the right song, so nothing is filled from it.
/// </param>
public sealed record TrackLookupResult(TrackMetadata? Match, TrackSuggestion? Suggestion)
{
    /// <summary>Nothing matched and nothing was close.</summary>
    public static TrackLookupResult None { get; } = new(null, null);
}
