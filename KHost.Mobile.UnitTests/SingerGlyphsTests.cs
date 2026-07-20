using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SingerGlyphsTests
{
    [Fact]
    public void All_has_no_duplicates_and_no_blanks()
    {
        Assert.Equal(SingerGlyphs.All.Count, SingerGlyphs.All.Distinct().Count());
        Assert.All(SingerGlyphs.All, g => Assert.False(string.IsNullOrWhiteSpace(g)));
    }

    [Fact]
    public void All_offers_a_curated_grid()
    {
        Assert.InRange(SingerGlyphs.All.Count, 24, 40);
    }
}
