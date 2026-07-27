using KHost.Mobile.Abstractions.Clients.Matching;

namespace KHost.Mobile.Abstractions.Clients.Metadata;

/// <summary>
/// Asks a second catalogue whether a song that matched nowhere looks misspelled. Keyless. A FALLBACK behind
/// <see cref="ITrackMetadataLookup"/>, which offers its own correction from a call that's already being made —
/// this is consulted only when the primary lookup found neither a match nor a near-miss, which usually means
/// the song simply isn't in its catalogue.
/// </summary>
public interface ISpellingSuggestionLookup
{
    /// <summary>
    /// The catalogue's spelling of <paramref name="title"/> by <paramref name="artist"/> when one result reads
    /// as a misspelling of them, else null.
    /// <para>Never throws: this is a hint, so a network failure, a rate limit or an unusable payload all
    /// degrade to "no suggestion" rather than surfacing to the caller.</para>
    /// </summary>
    Task<TrackSuggestion?> SuggestAsync(string title, string artist, CancellationToken cancellationToken = default);
}
