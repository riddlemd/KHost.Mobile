namespace KHost.Mobile.Clients.Updates;

/// <summary>
/// Finds the newest published version of the app. Keyless. The check is a best-effort nicety: it returns
/// <c>null</c> on any network/HTTP failure rather than throwing.
/// </summary>
public interface IUpdateLookup
{
    /// <summary>
    /// Fetch the newest published release (highest version, pre-releases included since all current builds
    /// are previews). Returns <c>null</c> when the feed is empty, unparseable, or unreachable.
    /// </summary>
    Task<ReleaseInfo?> GetNewestReleaseAsync(CancellationToken cancellationToken = default);
}
