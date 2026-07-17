namespace KHost.Mobile.Models;

/// <summary>
/// Pure geo helpers for "which venue am I at?" — great-circle distance and nearest-venue selection over a device
/// fix. No MAUI, no I/O, so the auto-select logic behind the location feature is unit-testable.
/// </summary>
public static class VenueProximity
{
    private const double EarthRadiusMeters = 6_371_000.0;

    /// <summary>Great-circle (haversine) distance in metres between two lat/lon points.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
              + (Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// The saved venue nearest to <paramref name="from"/> whose distance is within <paramref name="maxMeters"/>, or
    /// null when none qualifies. Venues without saved coordinates are skipped; ties resolve to the first-encountered.
    /// </summary>
    public static Venue? Nearest(GeoPoint from, IEnumerable<Venue> venues, double maxMeters)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(venues);

        Venue? best = null;
        var bestDistance = double.MaxValue;
        foreach (var v in venues)
        {
            if (v.Latitude is not double lat || v.Longitude is not double lon)
                continue;

            var d = DistanceMeters(from.Latitude, from.Longitude, lat, lon);
            if (d <= maxMeters && d < bestDistance)
            {
                bestDistance = d;
                best = v;
            }
        }
        return best;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
