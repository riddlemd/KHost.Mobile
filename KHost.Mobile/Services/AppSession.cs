namespace KHost.Mobile.Services;

/// <summary>In-memory <see cref="IAppSession"/> — plain mutable flags, no persistence. Registered as a singleton.</summary>
public sealed class AppSession : IAppSession
{
    /// <inheritdoc />
    public bool LandingResolved { get; set; }
}
