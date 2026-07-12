namespace KHost.Mobile.Client.Updates;

/// <summary>
/// A single published GitHub release, projected to just what the app needs to prompt an update.
/// <paramref name="Version"/> is the tag with any leading <c>v</c> and pre-release/build suffix stripped
/// (e.g. tag <c>v0.4.0</c> → <c>0.4.0</c>), ready to parse with <see cref="System.Version"/>.
/// </summary>
public sealed record ReleaseInfo(string Version, string? Name, string HtmlUrl, bool IsPrerelease);
