namespace KHost.Mobile.Clients.Matching;

/// <summary>
/// A catalogue title/artist offered as the correct spelling of what the user typed. Produced by any lookup
/// source that found a near-miss rather than a match.
/// </summary>
/// <param name="Title">The catalogue's spelling of the title.</param>
/// <param name="Artist">The catalogue's spelling of the artist.</param>
public sealed record TrackSuggestion(string Title, string Artist);
