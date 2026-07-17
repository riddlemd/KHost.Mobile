using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class VenueProximityTests
{
    [Fact]
    public void DistanceMeters_is_zero_for_the_same_point()
    {
        Assert.Equal(0, VenueProximity.DistanceMeters(34.09, -118.34, 34.09, -118.34), precision: 6);
    }

    [Fact]
    public void DistanceMeters_one_degree_of_latitude_is_about_111km()
    {
        var d = VenueProximity.DistanceMeters(0, 0, 1, 0);

        Assert.InRange(d, 111_000, 111_400);   // ~111.19 km per degree of latitude
    }

    [Fact]
    public void Nearest_returns_the_closest_venue_within_range()
    {
        var here = new GeoPoint(34.0000, -118.0000);
        var near = new Venue { Name = "Near", Latitude = 34.0005, Longitude = -118.0005 };   // ~70 m
        var far = new Venue { Name = "Far", Latitude = 34.0100, Longitude = -118.0100 };      // ~1.4 km

        var result = VenueProximity.Nearest(here, [far, near], maxMeters: 200);

        Assert.Same(near, result);
    }

    [Fact]
    public void Nearest_returns_null_when_the_closest_venue_is_beyond_the_radius()
    {
        var here = new GeoPoint(34.0000, -118.0000);
        var far = new Venue { Name = "Far", Latitude = 34.0100, Longitude = -118.0100 };   // ~1.4 km

        Assert.Null(VenueProximity.Nearest(here, [far], maxMeters: 200));
    }

    [Fact]
    public void Nearest_picks_the_closest_of_clustered_venues_and_drops_ones_just_out_of_range()
    {
        // Small venues on one block: ~40 m and ~90 m north of the fix. With a tight 75 m gate the near one counts
        // and the just-out-of-range one doesn't — the scenario that a loose radius would get wrong.
        var here = new GeoPoint(34.0000, -118.0000);
        var near = new Venue { Name = "Near", Latitude = 34.00036, Longitude = -118.0000 };   // ~40 m
        var justOut = new Venue { Name = "JustOut", Latitude = 34.00081, Longitude = -118.0000 };  // ~90 m

        Assert.Same(near, VenueProximity.Nearest(here, [justOut, near], maxMeters: 75));
    }

    [Fact]
    public void Nearest_skips_venues_without_coordinates()
    {
        var here = new GeoPoint(34.0000, -118.0000);
        var noCoords = new Venue { Name = "No location" };
        var placed = new Venue { Name = "Placed", Latitude = 34.0005, Longitude = -118.0005 };

        Assert.Same(placed, VenueProximity.Nearest(here, [noCoords, placed], maxMeters: 200));
    }

    [Fact]
    public void Nearest_returns_null_for_an_empty_or_all_unplaced_list()
    {
        var here = new GeoPoint(34.0000, -118.0000);

        Assert.Null(VenueProximity.Nearest(here, [], maxMeters: 200));
        Assert.Null(VenueProximity.Nearest(here, [new Venue { Name = "No location" }], maxMeters: 200));
    }
}
