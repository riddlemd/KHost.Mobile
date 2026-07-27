using KHost.Mobile.Services;

namespace KHost.Mobile.Models;

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
