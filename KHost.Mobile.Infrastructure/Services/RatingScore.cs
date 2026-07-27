using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>Tunables for the Bayesian how-it-went star. The UI builds this from <see cref="IAppSettings"/>.</summary>
/// <param name="PriorWeight">The confidence weight <c>m</c>: how many sings a song needs before it's trusted on its
/// own average rather than pulled toward the list prior. Higher = more shrinkage for lightly-sung songs.</param>
/// <param name="RecencyEnabled">When true, recent sings count more than old ones (exponential half-life decay).</param>
/// <param name="HalfLifeDays">The recency half-life in days — a sing this old counts half as much. Ignored when
/// <see cref="RecencyEnabled"/> is false.</param>
public sealed record RatingConfig(double PriorWeight, bool RecencyEnabled, double HalfLifeDays)
{
    /// <summary>m = 3 sings to trust a song on its own; recency off; 180-day (six-month) half-life when it's on.</summary>
    public static RatingConfig Default { get; } = new(PriorWeight: 3, RecencyEnabled: false, HalfLifeDays: 180);
}

/// <summary>
/// The list-wide context a star is computed against — chiefly the prior mean <c>C</c> (the whole list's average
/// how-it-went). Built once per list via <see cref="RatingScore.BuildContext"/> and reused for every song, so the
/// corpus is only walked once.
/// </summary>
/// <param name="PriorMean">The recency-weighted average how-it-went across every rated sing in the list, or null when
/// nothing has been rated yet (then every song's star is null too).</param>
/// <param name="Config">The tunables the stars were built with.</param>
/// <param name="Now">The reference time recency decay is measured from (passed in, not read, so scoring is pure).</param>
public sealed record RatingContext(double? PriorMean, RatingConfig Config, DateTimeOffset Now);

/// <summary>
/// Confidence-weighted "how it went" scoring. A song's star is a Bayesian shrinkage of its own average toward the
/// whole list's average: a song with few sings is pulled toward the list norm, one with many sings trusts its own
/// record — so ten solid 4.5s outrank a single lucky 5 on a normally-mixed list. Optionally weights recent sings more
/// (exponential half-life). Pure and MAUI-free, derived from the existing <see cref="SongListItem.Performances"/>, so
/// there's no stored field and no migration.
/// </summary>
public static class RatingScore
{
    /// <summary>
    /// Build the shared context — the prior mean — from the whole list, walking every rated performance once.
    /// <see cref="RatingContext.PriorMean"/> is the (recency-weighted) grand mean of every rated sing, or null when
    /// nothing has been rated yet.
    /// </summary>
    public static RatingContext BuildContext(IEnumerable<SongListItem> songs, RatingConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(songs);
        ArgumentNullException.ThrowIfNull(config);

        double weightSum = 0;
        double weightedValueSum = 0;
        foreach (var song in songs)
        {
            foreach (var performance in song.Performances)
            {
                if (performance.HowItWent < 1)
                    continue;   // logged-but-unrated sings don't count (mirrors AverageHowItWent)

                var weight = Weight(performance, config, now);
                weightSum += weight;
                weightedValueSum += weight * performance.HowItWent;
            }
        }

        double? prior = weightSum > 0 ? weightedValueSum / weightSum : null;
        return new RatingContext(prior, config, now);
    }

    /// <summary>
    /// The Bayesian star for one song against a <paramref name="context"/> from <see cref="BuildContext"/>. Null when
    /// the song has no (effective) rated sings, or the whole list has no prior — matching the "no star until rated"
    /// behavior of the old average.
    /// </summary>
    public static double? StarFor(SongListItem song, RatingContext context)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(context);

        if (context.PriorMean is not { } prior)
            return null;

        double effectiveCount = 0;
        double weightedValueSum = 0;
        foreach (var performance in song.Performances)
        {
            if (performance.HowItWent < 1)
                continue;

            var weight = Weight(performance, context.Config, context.Now);
            effectiveCount += weight;
            weightedValueSum += weight * performance.HowItWent;
        }

        if (effectiveCount <= 0)
            return null;   // never (effectively) sung → no star

        var average = weightedValueSum / effectiveCount;
        var m = context.Config.PriorWeight;
        return (effectiveCount * average + m * prior) / (effectiveCount + m);
    }

    // Per-performance weight: 1 when recency is off, else exponential half-life decay on the sing's age. A sing in the
    // future (or just now) counts fully; one a half-life old counts 0.5, two half-lives 0.25, and so on.
    private static double Weight(Performance performance, RatingConfig config, DateTimeOffset now)
    {
        if (!config.RecencyEnabled)
            return 1.0;

        var ageDays = (now - performance.Date).TotalDays;
        if (ageDays <= 0)
            return 1.0;

        return Math.Pow(0.5, ageDays / config.HalfLifeDays);
    }
}
