using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.UnitTests.Abstractions.Models;

public class VenueTests
{
    [Fact]
    public void HasLocation_is_true_only_when_both_coordinates_are_set()
    {
        Assert.False(new Venue().HasLocation);
        Assert.False(new Venue { Latitude = 34.09 }.HasLocation);
        Assert.False(new Venue { Longitude = -118.34 }.HasLocation);
        Assert.True(new Venue { Latitude = 34.09, Longitude = -118.34 }.HasLocation);
    }
}
