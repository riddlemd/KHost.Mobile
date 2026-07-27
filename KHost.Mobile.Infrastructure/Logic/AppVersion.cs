using System.Diagnostics.CodeAnalysis;

namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// Parses the running app's OWN version string — <c>ApplicationDisplayVersion</c>, reaching the app as
/// <c>AppInfo.Current.VersionString</c> — into a <see cref="Version"/> for comparison against a release.
/// </summary>
/// <remarks>
/// Deliberately not the same rule as the GitHub tag normalizer, which also strips a leading <c>v</c>:
/// <list type="bullet">
/// <item>A <b>pre-release/build suffix</b> is tolerated (<c>0.14.0-beta</c> → <c>0.14.0</c>). Android's
/// <c>versionName</c> is free-form and a suffixed dev build is ordinary, so refusing one would silently
/// disable the update check on exactly the builds most likely to need it.</item>
/// <item>A leading <b><c>v</c> is NOT</b> stripped. That is a git-tag convention; iOS requires
/// <c>CFBundleShortVersionString</c> to be period-separated digits, so <c>v0.14.0</c> would be an invalid
/// display version rather than something to accommodate.</item>
/// </list>
/// The overlap with <c>VersionTag</c> is two lines and stays duplicated on purpose: that type is internal to
/// the GitHub backend, and Clients sits below Infrastructure — sharing it would invert the layering to save
/// an <c>IndexOfAny</c>.
/// </remarks>
public static class AppVersion
{
    /// <summary>
    /// Parses <paramref name="displayVersion"/> into <paramref name="version"/>, ignoring any
    /// <c>-prerelease</c>/<c>+build</c> suffix. Returns false when what's left isn't a dotted numeric version
    /// — including for null, empty or whitespace input.
    /// </summary>
    public static bool TryParse(string? displayVersion, [NotNullWhen(true)] out Version? version)
    {
        version = null;

        var s = (displayVersion ?? string.Empty).Trim();
        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0)
            s = s[..cut];

        if (!Version.TryParse(s, out var parsed))
            return false;

        version = parsed;
        return true;
    }
}
