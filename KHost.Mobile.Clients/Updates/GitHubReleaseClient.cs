namespace KHost.Mobile.Clients.Updates;

/// <inheritdoc />
/// <remarks>
/// Hits the anonymous GitHub REST API (<c>api.github.com/repos/{owner}/{repo}/releases</c>). The base
/// address, the mandatory <c>User-Agent</c>, and the <c>Accept: application/vnd.github+json</c> header are
/// configured on the injected <see cref="HttpClient"/> at registration (keeping this library MAUI-free).
/// </remarks>
public sealed class GitHubReleaseClient(HttpClient httpClient) : IUpdateClient
{
    // Newest ~20 releases is plenty to find the highest version; the feed is small and already newest-first.
    private const string ReleasesPath = "repos/riddlemd/KHost.Mobile/releases?per_page=20";

    public async Task<ReleaseInfo?> GetNewestReleaseAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(ReleasesPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Check first: a caller cancel must still propagate. Anything else degrades to "nothing new".
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        // Dispose on every exit (including the not-success early return) so the pooled connection is released.
        using var _ = response;

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return GitHubReleaseParser.ParseNewest(json);
    }
}
