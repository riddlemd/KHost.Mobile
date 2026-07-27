namespace KHost.Mobile.Abstractions.Clients.Updates;

/// <summary>A single published release, projected to the fields the update check needs.</summary>
/// <param name="Version">The release tag already normalized and parsed, so callers compare rather than re-parse.
/// (The normalizing is <c>VersionTag</c> in KHost.Mobile.Common — not referenced here, since Abstractions
/// depends on nothing.)</param>
/// <param name="Name">Release display name, or null.</param>
/// <param name="HtmlUrl">The release's public page.</param>
/// <param name="IsPrerelease">True for a pre-release.</param>
public sealed record ReleaseInfo(Version Version, string? Name, string HtmlUrl, bool IsPrerelease);
