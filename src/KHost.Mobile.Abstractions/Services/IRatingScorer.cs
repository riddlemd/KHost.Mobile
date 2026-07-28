using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Confidence-weighted "how it went" scoring. A song's star is a Bayesian shrinkage of its own average toward the
/// whole list's average, so ten solid 4.5s outrank a single lucky 5.
/// </summary>
public interface IRatingScorer
{
    /// <summary>Builds the shared prior from the whole list, walking every rated performance once.</summary>
    RatingContext BuildContext(IEnumerable<SongListItem> songs, RatingConfig config, DateTimeOffset now);

    /// <summary>A song's blended star, or null when it has nothing rated.</summary>
    double? StarFor(SongListItem song, RatingContext context);
}
