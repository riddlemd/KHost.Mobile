using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// In-memory <see cref="IAppSession"/> — plain mutable flags, registered as a singleton. The one exception is the
/// active venue, which writes through to <paramref name="settings"/> so a manual pin outlives the process; the
/// launch bootstrap reads it back. Settings are optional so a test can <c>new</c> this bare and stay in memory.
/// </summary>
internal sealed class AppSession(IAppSettings? settings = null) : IAppSession
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
    public void ClearMySongsView(Guid singerId) => _mySongsViews.Remove(singerId);

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

        // Unconditional, not gated on venueChanged: flipping Auto/Pinned on the same venue changes nothing but the
        // pin, and that flip is exactly what has to survive a relaunch.
        if (settings is not null)
        {
            settings.LastActiveVenueId = venueId?.ToString() ?? string.Empty;
            settings.LastActiveVenuePinned = pinned;
        }

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
