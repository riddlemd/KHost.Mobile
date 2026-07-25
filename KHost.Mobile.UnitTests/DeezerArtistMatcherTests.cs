using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class DeezerArtistMatcherTests
{
    [Theory]
    [InlineData("The White Stripes", "White Stripes")]         // stop-word drop + set equality
    [InlineData("Ben Folds Five", "Ben Folds")]                 // subset, shorter side has 2+ tokens
    [InlineData("Daryl Hall & John Oates", "Hall & Oates")]
    [InlineData("Queen", "Queen")]                               // set equality needs no minimum token count
    public void Matches_real_variants(string resultArtist, string wantArtist)
        => Assert.True(DeezerArtistMatcher.Matches(resultArtist, wantArtist));

    [Theory]
    [InlineData("Prince Royce", "Prince")]   // single-token subset is rejected
    [InlineData("Bo Hazard", "Bo Burnham")]  // shares only a first name
    public void Rejects_unrelated_or_single_token_subset_artists(string resultArtist, string wantArtist)
        => Assert.False(DeezerArtistMatcher.Matches(resultArtist, wantArtist));

    [Theory]
    [InlineData(null, "Queen")]
    [InlineData("Queen", null)]
    [InlineData(null, null)]
    [InlineData("", "Queen")]
    [InlineData("Queen", "   ")]
    public void Rejects_null_or_blank_on_either_side(string? resultArtist, string? wantArtist)
        => Assert.False(DeezerArtistMatcher.Matches(resultArtist, wantArtist));

    [Fact]
    public void Tokens_folds_the_ampersand_to_a_space()
        => Assert.True(DeezerArtistMatcher.Tokens("Hall & Oates").SetEquals(["hall", "oates"]));
}
