using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class VenueGlyphsTests
{
    [Fact]
    public void All_leads_with_the_default_and_has_no_duplicates()
    {
        Assert.Equal(VenueGlyphs.Default, VenueGlyphs.All[0]);
        Assert.Equal(VenueGlyphs.All.Count, VenueGlyphs.All.Distinct().Count());
    }

    [Fact]
    public void All_offers_a_curated_set_of_around_thirty()
    {
        Assert.InRange(VenueGlyphs.All.Count, 24, 36);
        Assert.All(VenueGlyphs.All, g => Assert.False(string.IsNullOrWhiteSpace(g)));
    }
}
