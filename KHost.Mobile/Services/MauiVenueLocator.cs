using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="IVenueLocator"/> tying the location fix to the saved venues and the session's active-venue pointer.
/// The gating (opt-in + manual pin) lives here so callers — launch, the periodic re-check, and the manual
/// "re-check now" — can all just call <see cref="ResolveActiveAsync"/>.
/// </summary>
public sealed class MauiVenueLocator(
    ILocationProvider location,
    IVenueStore venues,
    IAppSession session,
    IAppSettings settings,
    ILogger<MauiVenueLocator> logger) : IVenueLocator
{
    // Treat the singer as "at" a venue within this radius of its saved point. Venues are small (a ~15 m / 50 ft
    // footprint) and often cluster on one block, so keep this tight — just the footprint plus typical medium-accuracy
    // GPS wobble — rather than a city-block radius that would flag a venue you're merely walking past. This is only
    // the "close enough to count" gate; among venues that are both in range, nearest-wins still picks the closest.
    private const double AtVenueMeters = 75;

    public async Task ResolveActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.LocationAutoDetect || session.ActiveVenuePinned)
            return;

        // Nothing to match against until at least one venue has a saved point, so short-circuit before touching the
        // device — this is what keeps us from asking for (or reading) location on a list with no geolocated venues.
        var saved = await venues.GetAllAsync();
        if (!saved.Any(v => v.HasLocation))
            return;

        var here = await location.GetCurrentAsync(cancellationToken);
        if (here is null)
            return;

        var nearest = VenueProximity.Nearest(here, saved, AtVenueMeters);
        if (nearest is not null && session.ActiveVenueId != nearest.Id)
        {
            logger.LogDebug("Auto-selected venue {Venue} from current location", nearest.Name);
            session.SetActiveVenue(nearest.Id, pinned: false);
        }
    }
}
