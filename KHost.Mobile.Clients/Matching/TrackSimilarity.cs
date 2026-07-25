namespace KHost.Mobile.Clients.Matching;

/// <summary>
/// Decides whether a search result that ISN'T an exact match is close enough to be a typo in what the user
/// typed, rather than a different song. Pure — no network.
/// </summary>
/// <remarks>
/// Tuned to under-report. Most of a real library legitimately isn't in the catalogue, so a loose rule would
/// offer a "did you mean" on dozens of correctly-spelled songs and train the user to ignore the hint.
/// </remarks>
internal static class TrackSimilarity
{
    /// <summary>Most edits we'll still call a typo.</summary>
    private const int MaxEdits = 2;

    /// <summary>
    /// An edit has to be a small fraction of the word to be a slip rather than a different word: at 1/5 this
    /// needs 5+ chars for one edit and 10+ for two, so "Yes"/"Yet" and "Africa"/"America" stay separate songs.
    /// </summary>
    private const int MinLengthPerEdit = 5;

    /// <summary>
    /// How many edits separate <paramref name="candidate"/> from <paramref name="wanted"/> when the gap is
    /// small enough to read as a misspelling, else null. Both are expected to be
    /// <see cref="TrackTextNormalizer.Normalize"/>d already — this measures spelling, not formatting.
    /// <para>Zero edits returns null too: an identical string is a match, not a correction.</para>
    /// </summary>
    public static int? TypoEdits(string candidate, string wanted)
        => EditsWithin(candidate, wanted, MaxEdits) is { } edits
            && edits > 0
            && edits * MinLengthPerEdit <= Math.Max(candidate.Length, wanted.Length)
                ? edits
                : null;

    /// <summary>
    /// Levenshtein distance, or null once it's provably greater than <paramref name="max"/>. Bailing early
    /// keeps a scan over a page of search results cheap and is the only reason the quadratic fill is fine here.
    /// </summary>
    private static int? EditsWithin(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
            return null;
        if (a == b)
            return 0;

        // Two rows are enough: filling row i only ever reads row i-1.
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var bestInRow = current[0];

            for (var j = 1; j <= b.Length; j++)
            {
                var substitute = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitute);
                bestInRow = Math.Min(bestInRow, current[j]);
            }

            // Every later row is >= the best of this one, so the answer can only grow from here.
            if (bestInRow > max)
                return null;

            (previous, current) = (current, previous);
        }

        var distance = previous[b.Length];
        return distance <= max ? distance : null;
    }
}
