using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.UnitTests.Abstractions.Models;

public class VenueGlyphsTests
{
    [Fact]
    public void All_leads_with_the_default_and_has_no_duplicates_or_blanks()
    {
        // Default must lead: the picker highlights All[0] for a venue that never chose a glyph.
        Assert.Equal(VenueGlyphs.Default, VenueGlyphs.All[0]);
        Assert.Equal(VenueGlyphs.All.Count, VenueGlyphs.All.Distinct().Count());
        Assert.All(VenueGlyphs.All, g => Assert.False(string.IsNullOrWhiteSpace(g)));
    }
}
