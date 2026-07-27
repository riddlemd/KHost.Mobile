using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.UI.Services;

/// <summary>
/// Bridges the app's settings to the Clients library's <see cref="ILookupOptions"/>, which can't reference
/// <see cref="IAppSettings"/> itself — that library is deliberately dependency-free.
/// </summary>
public sealed class AppLookupOptions(IAppSettings settings) : ILookupOptions
{
    /// <inheritdoc />
    public string CatalogueRegion => settings.CatalogueRegion;
}
