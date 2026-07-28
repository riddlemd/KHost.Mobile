using System.Diagnostics.CodeAnalysis;

namespace KHost.Mobile.Clients.GitHub;

/// <summary>
/// Normalizes a GitHub release tag into a <see cref="Version"/>. Pure — no network. Internal because the
/// only thing that produces tags is <see cref="GitHubReleaseParser"/>: the app's OWN version string comes from
/// ApplicationDisplayVersion, which is always plain dotted numerics and needs no stripping.
/// </summary>
internal static class VersionTag
{
    /// <summary>
    /// Parses <paramref name="tag"/> into <paramref name="version"/>, stripping a leading <c>v</c>/<c>V</c>
    /// and any <c>-prerelease</c>/<c>+build</c> suffix (<c>v0.4.0-beta.1</c> → <c>0.4.0</c>). Returns false
    /// when what's left isn't a dotted numeric version.
    /// </summary>
    public static bool TryParse(string tag, [NotNullWhen(true)] out Version? version)
    {
        version = null;

        var s = tag.Trim();
        if (s.Length > 0 && (s[0] is 'v' or 'V'))
            s = s[1..];

        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0)
            s = s[..cut];

        if (!Version.TryParse(s, out var parsed))
            return false;

        version = parsed;
        return true;
    }
}
