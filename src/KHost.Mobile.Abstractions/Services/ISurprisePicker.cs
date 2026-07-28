using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>The "what should I sing?" draw: narrows the pool by the user's rules, then picks one.</summary>
public interface ISurprisePicker
{
    /// <summary>
    /// Applies each narrowing rule, skipping any that would empty the pool — one overly strict rule shouldn't
    /// cancel the whole roll.
    /// </summary>
    IReadOnlyList<SongListItem> Narrow(IReadOnlyList<SongListItem> pool, SurpriseOptions options, DateTime today);

    /// <summary>
    /// Picks one song. <paramref name="roll"/> is a 0-1 value; the same roll against the same candidates always
    /// yields the same song, which is what keeps a draw reproducible in a test. Null only for an empty pool.
    /// </summary>
    SongListItem? Pick(
        IReadOnlyList<SongListItem> candidates,
        SurpriseOptions options,
        double roll,
        Func<SongListItem, double?> starFor,
        double neutralStar);
}
