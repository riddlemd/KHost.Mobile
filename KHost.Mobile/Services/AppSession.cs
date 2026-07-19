namespace KHost.Mobile.Services;

/// <summary>In-memory <see cref="IAppSession"/> — plain mutable flags, no persistence. Registered as a singleton.</summary>
public sealed class AppSession : IAppSession
{
    /// <inheritdoc />
    public bool LandingResolved { get; set; }

    /// <inheritdoc />
    public bool TutorialResolved { get; set; }

    // One My Songs view-state per singer id (created on first ask), plus a shared fallback for the null-singer path.
    private readonly Dictionary<Guid, MySongsViewState> _mySongsViews = [];
    private readonly MySongsViewState _mySongsViewNoSinger = new();

    /// <inheritdoc />
    public MySongsViewState MySongsViewFor(Guid? singerId)
    {
        if (singerId is not { } id)
            return _mySongsViewNoSinger;
        if (!_mySongsViews.TryGetValue(id, out var view))
            _mySongsViews[id] = view = new MySongsViewState();
        return view;
    }

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

    /// <inheritdoc />
    public Guid? ActiveSingerId { get; private set; }

    /// <inheritdoc />
    public event EventHandler? ActiveSingerChanged;

    /// <inheritdoc />
    public void SetActiveSinger(Guid? singerId)
    {
        if (ActiveSingerId == singerId)
            return;
        ActiveSingerId = singerId;
        ActiveSingerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Guid? TutorialVenueDetailId { get; private set; }

    /// <inheritdoc />
    public event EventHandler? TutorialVenueDetailChanged;

    /// <inheritdoc />
    public void SetTutorialVenueDetail(Guid? venueId)
    {
        if (TutorialVenueDetailId == venueId)
            return;
        TutorialVenueDetailId = venueId;
        TutorialVenueDetailChanged?.Invoke(this, EventArgs.Empty);
    }
}
