namespace KHost.Mobile.Services;

/// <summary>
/// Resolves the active venue from the device's location — the "auto-switch as you move between venues" behavior.
/// Orchestrates <see cref="ILocationProvider"/> + <see cref="IVenueStore"/> + <see cref="IAppSession"/>.
/// </summary>
public interface IVenueLocator
{
    /// <summary>
    /// Re-resolve the active venue from the current location and set it to the nearest saved venue within range.
    /// A no-op when location auto-detect is off (<see cref="IAppSettings.LocationAutoDetect"/>) or the active venue
    /// is pinned to a manual choice (<see cref="IAppSession.ActiveVenuePinned"/>). Best-effort — never throws; if no
    /// venue is in range the current active venue is left as-is (moving between venues switches; leaving them all
    /// doesn't clear it).
    /// </summary>
    Task ResolveActiveAsync(CancellationToken cancellationToken = default);
}
