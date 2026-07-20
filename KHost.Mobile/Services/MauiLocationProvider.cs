using KHost.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace KHost.Mobile.Services;

/// <summary>
/// <see cref="ILocationProvider"/> over MAUI Geolocation. Permission is requested lazily on first use; every failure
/// path (denied, disabled, timeout) degrades to <c>null</c> rather than throwing. The whole lookup is marshalled to
/// the main thread since the permission prompt requires it.
/// </summary>
public sealed class MauiLocationProvider(ILogger<MauiLocationProvider> logger) : ILocationProvider
{
    // A medium-accuracy fix is plenty to tell venues apart and is faster / lighter than best-accuracy GPS.
    private static readonly GeolocationRequest Request = new(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

    public async Task<GeoPoint?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    logger.LogDebug("Location permission not granted ({Status})", status);
                    return null;
                }

                var location = await Geolocation.Default.GetLocationAsync(Request, cancellationToken)
                               ?? await Geolocation.Default.GetLastKnownLocationAsync();
                if (location is null)
                {
                    logger.LogDebug("No location fix available");
                    return null;
                }

                return new GeoPoint(location.Latitude, location.Longitude);
            });
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            logger.LogWarning(ex, "Location unavailable");
            return null;
        }
        catch (Exception ex)
        {
            // Includes a cancelled/timed-out lookup — best-effort, so a null just means "no fix this time".
            logger.LogWarning(ex, "Location lookup failed");
            return null;
        }
    }
}
