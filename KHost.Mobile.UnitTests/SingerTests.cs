using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SingerTests
{
    [Fact]
    public void Defaults_to_the_brand_color_and_no_glyph()
    {
        var s = new Singer();
        Assert.Equal(SingerColors.Default, s.Color);
        Assert.Null(s.Glyph);
    }

    [Theory]
    [InlineData("Mike", "M")]
    [InlineData("sam", "S")]          // uppercased
    [InlineData("  Jordan", "J")]     // leading whitespace ignored
    [InlineData("élodie", "É")]       // non-ASCII first letter
    public void Initial_is_the_uppercased_first_letter(string name, string expected)
    {
        Assert.Equal(expected, new Singer { Name = name }.Initial);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Initial_falls_back_to_a_question_mark_when_the_name_is_blank(string name)
    {
        Assert.Equal("?", new Singer { Name = name }.Initial);
    }

    [Fact]
    public void Avatar_prefers_the_glyph_when_set()
    {
        Assert.Equal("🦄", new Singer { Name = "Sam", Glyph = "🦄" }.Avatar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Avatar_falls_back_to_the_initial_when_the_glyph_is_blank(string? glyph)
    {
        Assert.Equal("J", new Singer { Name = "Jordan", Glyph = glyph }.Avatar);
    }
}
