namespace KHost.Mobile.Clients.Updates;

/// <summary>A single published GitHub release, projected to just what the app needs to prompt an update.</summary>
/// <param name="Version">The tag with any leading <c>v</c> and pre-release/build suffix stripped (e.g. tag
/// <c>v0.4.0</c> → <c>0.4.0</c>), ready to parse with <see cref="System.Version"/>.</param>
/// <param name="Name">Release display name, or null.</param>
/// <param name="HtmlUrl">The release's web page — the target of the one-tap update link.</param>
/// <param name="IsPrerelease">True for a GitHub pre-release.</param>
public sealed record ReleaseInfo(string Version, string? Name, string HtmlUrl, bool IsPrerelease);
