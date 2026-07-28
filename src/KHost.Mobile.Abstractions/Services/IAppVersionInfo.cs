namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// The running build's own version, as configured by <c>ApplicationDisplayVersion</c>.
/// </summary>
/// <remarks>
/// Named this rather than <c>IAppInfo</c> because MAUI Essentials' global usings already bring
/// <c>Microsoft.Maui.ApplicationModel.IAppInfo</c> into scope, and reusing that name would collide — the same
/// trap <see cref="IHaptics"/> documents.
/// </remarks>
public interface IAppVersionInfo
{
    /// <summary>The display version string, e.g. <c>0.14.0</c>. Parse it with <c>AppVersion.TryParse</c>.</summary>
    string DisplayVersion { get; }
}
