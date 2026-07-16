using System.Text.Json;
using KHost.Mobile.Clients.Json;

namespace KHost.Mobile.Clients.Updates;

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
                if (tag is null || htmlUrl is null || !TryParseVersion(tag, out var version, out var clean))
                    continue;

                // The feed is ordered newest-first, but pick by parsed version so ordering can't fool us.
                if (bestVersion is null || version > bestVersion)
                {
                    bestVersion = version;
                    best = new ReleaseInfo(clean, release.Str("name"), htmlUrl, release.Bool("prerelease"));
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Turn a release tag / version string into a <see cref="System.Version"/>: strips a leading <c>v</c>/<c>V</c>
    /// and any <c>-prerelease</c>/<c>+build</c> suffix (e.g. <c>v0.4.0-beta.1</c> → <c>0.4.0</c>). Returns false
    /// when what's left isn't a dotted numeric version.
    /// </summary>
    public static bool TryParseVersion(string tag, out Version version, out string clean)
    {
        version = new Version(0, 0);
        clean = string.Empty;

        var s = tag.Trim();
        if (s.Length > 0 && (s[0] is 'v' or 'V'))
            s = s[1..];

        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0)
            s = s[..cut];

        if (Version.TryParse(s, out var parsed))
        {
            version = parsed;
            clean = s;
            return true;
        }

        return false;
    }
}
