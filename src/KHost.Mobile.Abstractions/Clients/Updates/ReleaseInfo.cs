namespace KHost.Mobile.Abstractions.Clients.Updates;

/// <summary>A single published release, projected to the fields the update check needs.</summary>
/// <param name="Version">The release tag already normalized and parsed — a leading <c>v</c> and any
/// pre-release/build suffix stripped — so callers compare rather than re-parse. Which backend did the
/// normalizing is deliberately not named here: Abstractions depends on nothing.</param>
/// <param name="Name">Release display name, or null.</param>
/// <param name="HtmlUrl">The release's public page.</param>
/// <param name="IsPrerelease">True for a pre-release.</param>
public sealed record ReleaseInfo(Version Version, string? Name, string HtmlUrl, bool IsPrerelease);
