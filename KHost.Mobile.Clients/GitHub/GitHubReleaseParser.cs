using System.Text.Json;
using KHost.Mobile.Abstractions.Clients.Updates;
using KHost.Mobile.Common.Json;


namespace KHost.Mobile.Clients.GitHub;

/// <summary>
/// Parses a GitHub <c>/releases</c> response (a JSON array of release objects) and returns the newest
/// non-draft release by version. Pure — no network. Each object looks like
/// <c>{ tag_name, name, html_url, draft, prerelease, ... }</c>. Pre-releases are kept (all current builds
/// are previews); drafts are skipped. Returns <c>null</c> when nothing parses.
/// </summary>
public static class GitHubReleaseParser
{
    public static ReleaseInfo? ParseNewest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            ReleaseInfo? best = null;
            Version? bestVersion = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.Bool("draft"))
                    continue;

                var tag = release.Str("tag_name");
                var htmlUrl = release.Str("html_url");
                if (tag is null || htmlUrl is null || !VersionTag.TryParse(tag, out var version))
                    continue;

                // The feed is ordered newest-first, but pick by parsed version so ordering can't fool us.
                if (bestVersion is null || version > bestVersion)
                {
                    bestVersion = version;
                    best = new ReleaseInfo(version, release.Str("name"), htmlUrl, release.Bool("prerelease"));
                }
            }

            return best;
        }
    }
}
