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
    public bool ActiveVenuePinned { get; private set; }

    /// <inheritdoc />
    public event EventHandler? ActiveVenueChanged;

    /// <inheritdoc />
    public void SetActiveVenue(Guid? venueId, bool pinned = false)
    {
        var venueChanged = ActiveVenueId != venueId;
        ActiveVenueId = venueId;
        ActiveVenuePinned = pinned;   // always reflects the latest caller (e.g. "resume auto" unpins the same venue)
        if (venueChanged)
            ActiveVenueChanged?.Invoke(this, EventArgs.Empty);
    }
}
