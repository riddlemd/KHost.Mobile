using Microsoft.Extensions.Logging;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// <see cref="IVenueLocator"/> tying the location fix to the saved venues and the session's active-venue pointer.
/// The gating (opt-in + manual pin) lives here so callers — launch, the periodic re-check, and the manual
/// "re-check now" — can all just call <see cref="ResolveActiveAsync"/>.
/// </summary>
internal sealed class VenueLocator(
    ILocationProvider location,
    IVenueStore venues,
    IAppSession session,
    IAppSettings settings,
    ILogger<VenueLocator> logger) : IVenueLocator
{
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

        // Read per-resolve, not cached: the setting can change between re-checks while the app stays open.
        var nearest = VenueProximity.Nearest(here, saved, settings.VenueDetectionMeters);
        if (nearest is not null && session.ActiveVenueId != nearest.Id)
        {
            logger.LogDebug("Auto-selected venue {Venue} from current location", nearest.Name);
            session.SetActiveVenue(nearest.Id, pinned: false);
        }
    }
}
