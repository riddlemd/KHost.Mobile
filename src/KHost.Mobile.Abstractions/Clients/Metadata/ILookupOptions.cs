namespace KHost.Mobile.Abstractions.Clients.Metadata;

/// <summary>
/// Host-supplied preferences for catalogue lookups. Declared here rather than taking the app's settings type so
/// this library keeps its zero-dependency contract; the MAUI head implements it over its own settings store.
/// </summary>
/// <remarks>
/// Read per call, not cached, so a change made in Settings reaches the next lookup without a restart.
/// </remarks>
public interface ILookupOptions
{
    /// <summary>Two-letter store region a search runs against (ISO 3166-1 alpha-2, e.g. <c>US</c>, <c>GB</c>).</summary>
    string CatalogueRegion { get; }
}

/// <summary>The defaults used when a host supplies no <see cref="ILookupOptions"/>.</summary>
public sealed class DefaultLookupOptions : ILookupOptions
{
    /// <inheritdoc />
    public string CatalogueRegion => "US";
}
