using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KHost.Mobile.UI;

/// <summary>
/// Registers what the Razor UI implements. Only <see cref="IAlbumArtService"/> lives here — everything else the
/// components use arrives as an interface from a lower project.
/// </summary>
public static class Project
{
    /// <summary>Adds the album-art view service.</summary>
    /// <remarks>
    /// <b>Scoped, not singleton.</b> It talks to the WebView through <c>IJSRuntime</c>, which is scoped in Blazor
    /// Hybrid; a singleton captures a runtime that isn't attached to the live WebView and every transfer then
    /// fails silently. One <c>BlazorWebView</c> means the scoped instance is still effectively app-wide.
    /// </remarks>
    public static IServiceCollection AddKHostUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IAlbumArtService, AlbumArtService>();
        return services;
    }
}
