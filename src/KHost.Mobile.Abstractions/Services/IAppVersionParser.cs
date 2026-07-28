namespace KHost.Mobile.Abstractions.Services;

/// <summary>Parses the running app's own display version for comparison against a release.</summary>
public interface IAppVersionParser
{
    /// <summary>
    /// Parses a display version, ignoring any <c>-prerelease</c>/<c>+build</c> suffix. A leading <c>v</c> is NOT
    /// stripped — that's a git-tag convention, and iOS requires a period-separated numeric display version.
    /// </summary>
    bool TryParse(string? displayVersion, out Version? version);
}
