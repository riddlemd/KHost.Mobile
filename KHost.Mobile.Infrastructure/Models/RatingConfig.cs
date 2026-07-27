using KHost.Mobile.Services;

namespace KHost.Mobile.Models;

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
