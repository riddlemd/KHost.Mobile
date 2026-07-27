using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// Best-effort access to the device's current location, abstracted so the venue logic stays MAUI-free and the
/// permission/geolocation plumbing lives in one place. Backed by MAUI Geolocation on-device.
/// </summary>
public interface ILocationProvider
{
    /// <summary>
    /// The current device location, requesting the "when in use" location permission if it isn't granted yet.
    /// Returns <c>null</c> when permission is denied, location services are off/unavailable, or the lookup fails —
    /// it never throws (best-effort, like the other external ops).
    /// </summary>
    Task<GeoPoint?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
