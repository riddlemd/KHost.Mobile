using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class VenueTests
{
    [Fact]
    public void New_venue_defaults_to_the_mic_glyph()
    {
        Assert.Equal(VenueGlyphs.Default, new Venue().Glyph);
    }

    [Fact]
    public void HasLocation_is_true_only_when_both_coordinates_are_set()
    {
        Assert.False(new Venue().HasLocation);
        Assert.False(new Venue { Latitude = 34.09 }.HasLocation);
        Assert.False(new Venue { Longitude = -118.34 }.HasLocation);
        Assert.True(new Venue { Latitude = 34.09, Longitude = -118.34 }.HasLocation);
    }
}
