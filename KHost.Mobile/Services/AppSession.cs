namespace KHost.Mobile.Services;

/// <summary>In-memory <see cref="IAppSession"/> — plain mutable flags, no persistence. Registered as a singleton.</summary>
public sealed class AppSession : IAppSession
{
    /// <inheritdoc />
    public bool LandingResolved { get; set; }

    /// <inheritdoc />
    public bool TutorialResolved { get; set; }

    /// <inheritdoc />
    public MySongsViewState MySongsView { get; } = new();

    /// <inheritdoc />
    public Guid? ActiveVenueId { get; private set; }

    /// <inheritdoc />
    public event EventHandler? ActiveVenueChanged;

    /// <inheritdoc />
    public void SetActiveVenue(Guid? venueId)
    {
        if (ActiveVenueId == venueId)
            return;

        ActiveVenueId = venueId;
        ActiveVenueChanged?.Invoke(this, EventArgs.Empty);
    }
}
