using KHost.Mobile.Abstractions.Services;
using Microsoft.Maui.ApplicationModel;

namespace KHost.Mobile.Services;

/// <inheritdoc />
public sealed class MauiAppVersionInfo : IAppVersionInfo
{
    /// <inheritdoc />
    public string DisplayVersion => AppInfo.Current.VersionString;
}
