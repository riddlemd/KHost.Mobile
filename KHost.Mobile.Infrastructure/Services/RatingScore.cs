using KHost.Mobile.Infrastructure.Models;
using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.Infrastructure.Services;

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
