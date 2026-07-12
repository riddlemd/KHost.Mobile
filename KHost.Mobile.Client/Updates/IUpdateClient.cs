namespace KHost.Mobile.Client.Updates;

/// <summary>
/// Reads the app's GitHub Releases feed to find the newest published version. Keyless (anonymous GitHub
/// REST API). The check is a best-effort nicety: it returns <c>null</c> on any network/HTTP failure rather
/// than throwing.
/// </summary>
public interface IUpdateClient
{
    /// <summary>
    /// Fetch the newest published release (highest version, pre-releases included since all current builds
    /// are previews). Returns <c>null</c> when the feed is empty, unparseable, or unreachable.
    /// </summary>
    Task<ReleaseInfo?> GetNewestReleaseAsync(CancellationToken cancellationToken = default);
}
