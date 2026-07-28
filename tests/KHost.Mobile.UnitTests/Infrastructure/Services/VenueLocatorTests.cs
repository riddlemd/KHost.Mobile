using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// The auto-detect policy: turn a location fix into the active venue, or decline to. Every early return here is
// load-bearing — two of them are privacy guarantees (don't read location when the feature is off, or when
// there's nothing to match against) and one is the documented rule that a manual pick pins the venue so
// auto-detect can't override it. The fake location provider records whether it was ASKED, so "didn't read
// location" is asserted rather than assumed.
public class VenueLocatorTests
{
    private static readonly Guid NearId = Guid.NewGuid();
    private static readonly Guid FarId = Guid.NewGuid();

    private static Venue Located(Guid id, string name, double lat, double lon) =>
        new() { Id = id, Name = name, Latitude = lat, Longitude = lon };

    // Two venues ~1.1km apart; the fix below sits on top of "Near".
    private static IReadOnlyList<Venue> TwoVenues() =>
        [Located(NearId, "Near", 40.0000, -75.0000), Located(FarId, "Far", 40.0100, -75.0000)];

    private static readonly GeoPoint AtNear = new(40.0000, -75.0000);

    private static (VenueLocator locator, FakeLocation gps, AppSession session) Build(
        IReadOnlyList<Venue> venues, GeoPoint? fix, bool autoDetect = true, int radiusMeters = 250)
    {
        var gps = new FakeLocation(fix);
        var session = new AppSession();
        var locator = new VenueLocator(gps, new FakeVenueStore(venues), session,
            new FakeAppSettings { LocationAutoDetect = autoDetect, VenueDetectionMeters = radiusMeters },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VenueLocator>.Instance);
        return (locator, gps, session);
    }

    [Fact]
    public async Task Selects_the_nearest_venue_within_the_radius()
    {
        var (locator, _, session) = Build(TwoVenues(), AtNear);

        await locator.ResolveActiveAsync();

        Assert.Equal(NearId, session.ActiveVenueId);
        Assert.False(session.ActiveVenuePinned);   // auto-detected, so a later re-check may move it
    }

    [Fact]
    public async Task Never_reads_location_when_auto_detect_is_off()
    {
        // Privacy: the setting has to gate the device read itself, not just the result.
        var (locator, gps, session) = Build(TwoVenues(), AtNear, autoDetect: false);

        await locator.ResolveActiveAsync();

        Assert.False(gps.WasAsked);
        Assert.Null(session.ActiveVenueId);
    }

    [Fact]
    public async Task Never_reads_location_when_no_venue_has_one()
    {
        // Also privacy: with nothing to match against there is no reason to touch the device at all.
        var (locator, gps, session) = Build([new Venue { Id = Guid.NewGuid(), Name = "No point" }], AtNear);

        await locator.ResolveActiveAsync();

        Assert.False(gps.WasAsked);
        Assert.Null(session.ActiveVenueId);
    }

    [Fact]
    public async Task A_pinned_venue_is_never_overridden()
    {
        // The documented rule: a manual pick pins, and auto-detect must not move it — even standing on another.
        var (locator, gps, session) = Build(TwoVenues(), AtNear);
        session.SetActiveVenue(FarId, pinned: true);

        await locator.ResolveActiveAsync();

        Assert.False(gps.WasAsked);
        Assert.Equal(FarId, session.ActiveVenueId);
        Assert.True(session.ActiveVenuePinned);
    }

    [Fact]
    public async Task Leaves_the_active_venue_alone_when_there_is_no_fix()
    {
        var (locator, _, session) = Build(TwoVenues(), fix: null);
        session.SetActiveVenue(FarId);

        await locator.ResolveActiveAsync();

        Assert.Equal(FarId, session.ActiveVenueId);
    }

    [Fact]
    public async Task Selects_nothing_when_every_venue_is_outside_the_radius()
    {
        // Standing 1.1km away with a 250m radius: no venue is "here".
        var (locator, _, session) = Build([Located(FarId, "Far", 40.0100, -75.0000)], AtNear);

        await locator.ResolveActiveAsync();

        Assert.Null(session.ActiveVenueId);
    }

    [Fact]
    public async Task A_wider_radius_reaches_a_venue_a_narrow_one_misses()
    {
        var venues = new[] { Located(FarId, "Far", 40.0100, -75.0000) };

        var (narrow, _, narrowSession) = Build(venues, AtNear, radiusMeters: 250);
        await narrow.ResolveActiveAsync();

        var (wide, _, wideSession) = Build(venues, AtNear, radiusMeters: 5000);
        await wide.ResolveActiveAsync();

        Assert.Null(narrowSession.ActiveVenueId);
        Assert.Equal(FarId, wideSession.ActiveVenueId);
    }

    [Fact]
    public async Task Re_resolving_onto_the_same_venue_leaves_it_unpinned_and_unchanged()
    {
        var (locator, _, session) = Build(TwoVenues(), AtNear);

        await locator.ResolveActiveAsync();
        await locator.ResolveActiveAsync();

        Assert.Equal(NearId, session.ActiveVenueId);
        Assert.False(session.ActiveVenuePinned);
    }

    // ---- test doubles --------------------------------------------------------------------------

    private sealed class FakeLocation(GeoPoint? fix) : ILocationProvider
    {
        public bool WasAsked { get; private set; }

        public Task<GeoPoint?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            WasAsked = true;
            return Task.FromResult(fix);
        }
    }

    private sealed class FakeVenueStore(IReadOnlyList<Venue> venues) : IVenueStore
    {
        public event EventHandler? Changed { add { } remove { } }
        public Task<IReadOnlyList<Venue>> GetAllAsync() => Task.FromResult(venues);
        public Task<Venue?> GetAsync(Guid id) => Task.FromResult(venues.FirstOrDefault(v => v.Id == id));
        public Task<Venue> AddAsync(Venue venue) => Task.FromResult(venue);
        public Task UpdateAsync(Venue venue) => Task.CompletedTask;
        public Task RemoveAsync(Guid id) => Task.CompletedTask;
    }
}
