using KHost.Mobile.Abstractions.Clients.Matching;

namespace KHost.Mobile.Clients.Matching;

/// <summary>
/// Picks the one search result that reads as a misspelling of what was asked for. Pure — no network. Shared
/// by every lookup source so they all offer corrections on the same terms.
/// </summary>
internal static class TrackSuggestionFinder
{
    /// <summary>
    /// The closest near-miss among <paramref name="candidates"/>, or null when none is close enough.
    /// </summary>
    /// <param name="candidates">Raw title/artist pairs straight off the source, in result order.</param>
    /// <param name="wantTitle">The requested title, already normalized.</param>
    /// <param name="wantArtist">The requested artist, already normalized.</param>
    /// <param name="artistMatches">
    /// Whether a raw result artist counts as the requested one. A source with its own notion of an acceptable
    /// artist variant (Deezer's token comparison) passes that in, so a variant is never mistaken for a typo.
    /// </param>
    public static TrackSuggestion? Best(
        IEnumerable<(string? Title, string? Artist)> candidates,
        string wantTitle,
        string wantArtist,
        Func<string?, bool> artistMatches)
    {
        TrackSuggestion? best = null;
        var bestEdits = int.MaxValue;

        foreach (var (title, artist) in candidates)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                continue;

            // One side has to be spelled EXACTLY right and the other within a couple of edits: a typo is a slip
            // in one field, and demanding an anchor is what stops an unrelated song — or a same-titled cover by
            // another artist — from being proposed as the fix.
            var (typo, anchor) = (TrackTextNormalizer.Normalize(title) == wantTitle, artistMatches(artist)) switch
            {
                (true, false) => (TrackTextNormalizer.Normalize(artist), wantArtist),
                (false, true) => (TrackTextNormalizer.Normalize(title), wantTitle),
                _ => (null, null),           // both right is a match; both wrong is a different song
            };
            if (typo is null || anchor is null || TrackSimilarity.TypoEdits(typo, anchor) is not { } edits)
                continue;

            if (edits >= bestEdits)
                continue;

            bestEdits = edits;
            // Offer the plain song name: a typo search surfaces "Creep (Acoustic)" long before "Creep", and
            // writing the version suffix into the user's own title isn't the correction they asked for.
            best = new TrackSuggestion(TrackTextNormalizer.StripAsides(title), TrackTextNormalizer.StripAsides(artist));
        }

        return best;
    }
}
