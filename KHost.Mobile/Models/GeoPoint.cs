namespace KHost.Mobile.Models;

/// <summary>
/// A latitude/longitude pair — a device fix or a venue's saved location. Deliberately decoupled from MAUI's
/// <c>Location</c> so the proximity math (<see cref="VenueProximity"/>) stays a pure, testable model with no MAUI
/// dependency.
/// </summary>
public sealed record GeoPoint(double Latitude, double Longitude);
