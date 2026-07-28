using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Whether two artist strings name the same act. Deezer's artist string can be a superset/subset of ours
/// ("White Stripes" vs "The White Stripes", "Hall &amp; Oates" vs "Daryl Hall &amp; John Oates", "Ben Folds"
/// vs "Ben Folds Five").
/// </summary>
internal static class DeezerArtistMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal) { "the", "and", "a" };

    /// <summary>
    /// Accepts an exact token match, or a subset either way as long as the shorter side has ≥2 meaningful
    /// tokens — that keeps the real variants while rejecting a single-token coincidence ("Prince" ⊄ "Prince
    /// Royce") or a wrong artist that only shares a first name ("Bo Burnham" vs "Bo Hazard").
    /// </summary>
    public static bool Matches(string? resultArtist, string? wantArtist)
        => Matches(resultArtist, Tokens(wantArtist));

    /// <inheritdoc cref="Matches(string?, string?)" />
    public static bool Matches(string? resultArtist, HashSet<string> wantArtist)
    {
        var got = Tokens(resultArtist);
        if (got.Count == 0 || wantArtist.Count == 0)
            return false;
        if (got.SetEquals(wantArtist))
            return true;
        var (smaller, larger) = got.Count <= wantArtist.Count ? (got, wantArtist) : (wantArtist, got);
        return smaller.Count >= 2 && smaller.IsSubsetOf(larger);
    }

    public static HashSet<string> Tokens(string? value)
        => TrackTextNormalizer.Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t))
            .ToHashSet(StringComparer.Ordinal);
}
