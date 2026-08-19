using Xunit;

using KHost.Mobile.Abstractions.Models;
namespace KHost.Mobile.UnitTests.Abstractions.Models;

public class SingerGlyphsTests
{
    [Fact]
    public void All_is_a_non_empty_grid_with_no_duplicates_and_no_blanks()
    {
        // A duplicate or blank entry renders as a picker cell that can't be told apart from its neighbour.
        Assert.NotEmpty(SingerGlyphs.All);
        Assert.Equal(SingerGlyphs.All.Count, SingerGlyphs.All.Distinct().Count());
        Assert.All(SingerGlyphs.All, g => Assert.False(string.IsNullOrWhiteSpace(g)));
    }
}
