using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class GenresMapTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_returns_null_for_blank_input(string? input)
    {
        Assert.Null(Genres.Map(input));
    }

    [Theory]
    [InlineData("Rock", "Rock")]
    [InlineData("rock", "Rock")]                 // case-insensitive
    [InlineData("  POP  ", "Pop")]               // trimmed + case-insensitive, canonical casing returned
    [InlineData("hip hop", "Hip Hop")]
    [InlineData("r&b", "R&B")]
    public void Map_matches_a_known_genre_exactly_and_returns_canonical_casing(string input, string expected)
    {
        Assert.Equal(expected, Genres.Map(input));
    }

    [Theory]
    [InlineData("Hip-Hop/Rap", "Hip Hop")]
    [InlineData("Singer/Songwriter", "Singer-Songwriter")]
    [InlineData("Holiday", "Christmas / Holiday")]
    [InlineData("Pop-Punk", "Pop Punk")]
    [InlineData("Adult Contemporary", "Rock")]
    [InlineData("Comedy", "Musical Comedy")]
    public void Map_resolves_known_aliases(string input, string expected)
    {
        Assert.Equal(expected, Genres.Map(input));
    }

    [Theory]
    // The contains-fallback picks the first entry (in All order) that is a substring of the input.
    [InlineData("R&B/Soul", "R&B")]
    [InlineData("Alternative Rock", "Alternative")]
    [InlineData("Southern Rock", "Rock")]
    [InlineData("Smooth Jazz", "Jazz")]
    public void Map_falls_back_to_a_contained_genre(string input, string expected)
    {
        Assert.Equal(expected, Genres.Map(input));
    }

    [Theory]
    [InlineData("Polka")]
    [InlineData("Yodeling")]
    [InlineData("Spoken Word")]
    public void Map_returns_null_for_an_unrecognized_genre(string input)
    {
        Assert.Null(Genres.Map(input));
    }
}
