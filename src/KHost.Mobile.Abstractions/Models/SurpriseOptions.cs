namespace KHost.Mobile.Abstractions.Models;

/// <summary>
/// The options the "Surprise me" picker draws under. Mirrors the toggles on the picker's options sheet, which
/// persist through <see cref="IAppSettings"/>.
/// </summary>
/// <param name="SkipSungToday">Leave out songs already sung today (ignored if that empties the pool).</param>
/// <param name="FavourWellSung">Weight the draw by each song's how-it-went star instead of drawing evenly.</param>
/// <param name="NeverSungOnly">Draw only from songs with no performance history (ignored if that empties the pool).</param>
/// <param name="FavoritesOnly">Draw only from favorites (ignored if that empties the pool).</param>
public sealed record SurpriseOptions(
    bool SkipSungToday,
    bool FavourWellSung,
    bool NeverSungOnly,
    bool FavoritesOnly)
{
    /// <summary>The behaviour the picker had before the options sheet existed: filtered pool, star-weighted, skip today.</summary>
    public static SurpriseOptions Default { get; } =
        new(SkipSungToday: true, FavourWellSung: true, NeverSungOnly: false, FavoritesOnly: false);
}
