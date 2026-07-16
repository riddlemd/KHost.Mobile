using KHost.Mobile.Clients.Matching;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class TrackTextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Returns_empty_for_null_or_blank(string? input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("Aeroplane", "aeroplane")]
    [InlineData("SWEET Child", "sweet child")]   // lowercased
    [InlineData("  Trimmed  ", "trimmed")]       // outer whitespace gone
    [InlineData("Blink 182", "blink 182")]       // digits preserved
    public void Lowercases_trims_and_keeps_alphanumerics(string input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("Sweet Dreams (Are Made of This)", "sweet dreams")]   // parenthetical dropped
    [InlineData("Song [Live]", "song")]                               // bracketed dropped
    [InlineData("Song (Remastered 2011)", "song")]
    public void Drops_bracketed_and_parenthetical_asides(string input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("Song - Live", "song")]              // trailing " - " qualifier cut
    [InlineData("Artist feat. Someone", "artist")]
    [InlineData("Artist feat Someone", "artist")]
    [InlineData("Song featuring Guest", "song")]
    [InlineData("Song ft. Guest", "song")]
    [InlineData("Song ft Guest", "song")]
    public void Cuts_trailing_feature_and_version_qualifiers(string input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("Beyoncé", "beyonce")]
    [InlineData("Mötley Crüe", "motley crue")]
    public void Strips_accents(string input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("Wow, I Can Get Sexual Too", "wow i can get sexual too")]   // punctuation → space
    [InlineData("Rock & Roll", "rock roll")]
    [InlineData("A  B   C", "a b c")]                                        // whitespace collapsed
    public void Folds_punctuation_to_spaces_and_collapses_whitespace(string input, string expected)
        => Assert.Equal(expected, TrackTextNormalizer.Normalize(input));

    [Fact]
    public void The_same_song_written_two_ways_normalizes_equal()
        => Assert.Equal(
            TrackTextNormalizer.Normalize("Wow, I Can Get Sexual Too"),
            TrackTextNormalizer.Normalize("Wow I Can Get Sexual Too"));

    [Fact]
    public void Two_different_songs_do_not_collapse_to_the_same_value()
        => Assert.NotEqual(
            TrackTextNormalizer.Normalize("Africa"),
            TrackTextNormalizer.Normalize("Rosanna"));
}
