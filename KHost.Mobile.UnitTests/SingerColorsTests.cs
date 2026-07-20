using System.Text.RegularExpressions;
using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SingerColorsTests
{
    [Fact]
    public void All_leads_with_the_default_and_has_no_duplicates()
    {
        Assert.Equal(SingerColors.Default, SingerColors.All[0]);
        Assert.Equal(SingerColors.All.Count, SingerColors.All.Distinct().Count());
    }

    [Fact]
    public void All_are_six_digit_hex_colors()
    {
        Assert.All(SingerColors.All, c => Assert.Matches(new Regex("^#[0-9a-fA-F]{6}$"), c));
    }
}
