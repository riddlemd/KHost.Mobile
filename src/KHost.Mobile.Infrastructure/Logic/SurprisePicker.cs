using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// Chooses a song at random for "Surprise me". Pure and deterministic given its roll value, so the narrowing rules
/// and the weighting can be tested without a UI or an RNG.
/// </summary>
public static class SurprisePicker
{
    /// <summary>
    /// Narrows <paramref name="pool"/> by the option's restrictions. Each restriction is skipped when applying it
    /// would leave nothing to draw from — a picker that can return nothing is worse than one that ignores a
    /// preference, and the caller has no way to explain an empty result in a one-tap flow.
    /// </summary>
    public static IReadOnlyList<SongListItem> Narrow(
        IReadOnlyList<SongListItem> pool, SurpriseOptions options, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(options);

        var candidates = pool;
        if (options.FavoritesOnly)
            candidates = KeepIfAny(candidates, i => i.IsFavorite);
        if (options.NeverSungOnly)
            candidates = KeepIfAny(candidates, i => i.Performances.Count == 0);
        if (options.SkipSungToday)
            candidates = KeepIfAny(candidates, i => !SungOn(i, today));
        return candidates;
    }

    /// <summary>
    /// Picks one song. <paramref name="roll"/> is a 0-1 value (pass <see cref="Random.NextDouble"/>); the same roll
    /// against the same candidates always yields the same song. Returns null only for an empty pool.
    /// </summary>
    /// <param name="starFor">A song's how-it-went star, or null when unrated. Only consulted when weighting.</param>
    /// <param name="neutralStar">Star assigned to unrated songs so they aren't excluded — the list's average.</param>
    public static SongListItem? Pick(
        IReadOnlyList<SongListItem> candidates,
        SurpriseOptions options,
        double roll,
        Func<SongListItem, double?> starFor,
        double neutralStar)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(starFor);

        if (candidates.Count == 0)
            return null;

        roll = Math.Clamp(roll, 0, 0.999999);
        if (!options.FavourWellSung)
            return candidates[(int)(roll * candidates.Count)];

        // Every song keeps a floor so a run of bad nights can never make one undrawable.
        var weights = candidates.Select(c => Math.Max(MinWeight, starFor(c) ?? neutralStar)).ToList();
        var total = weights.Sum();
        if (total <= 0)
            return candidates[(int)(roll * candidates.Count)];

        var target = roll * total;
        for (var i = 0; i < candidates.Count; i++)
        {
            target -= weights[i];
            if (target <= 0)
                return candidates[i];
        }
        return candidates[^1];   // floating-point guard: the roll landed exactly on the total
    }

    private const double MinWeight = 0.25;

    private static IReadOnlyList<SongListItem> KeepIfAny(
        IReadOnlyList<SongListItem> pool, Func<SongListItem, bool> predicate)
    {
        var kept = pool.Where(predicate).ToList();
        return kept.Count > 0 ? kept : pool;
    }

    private static bool SungOn(SongListItem item, DateTime day) =>
        item.Performances.Any(p => p.Date.LocalDateTime.Date == day.Date);
}
