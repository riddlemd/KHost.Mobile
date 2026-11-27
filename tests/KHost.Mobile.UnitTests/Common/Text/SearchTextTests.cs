using KHost.Mobile.Common.Text;
using Xunit;

namespace KHost.Mobile.UnitTests.Common.Text;

public class SearchTextTests
{
    [Theory]
    [InlineData("Don't Stop Believin'", "dont stop believin")]
    [InlineData("Don’t  Stop!", "dont stop")]
    [InlineData("Beyoncé", "beyonce")]
    [InlineData("  AC/DC  ", "ac dc")]
    [InlineData("Mr. Brightside", "mr brightside")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Fold_ignores_case_accents_and_punctuation(string? input, string expected)
        => Assert.Equal(expected, SearchText.Fold(input));

    [Theory]
    [InlineData("Don't Stop Believin'", "dont stop belie", true)]   // half-typed, no apostrophe
    [InlineData("Mr. Brightside", "bri", true)]                      // the start of a word
    [InlineData("Mr. Brightside", "side", false)]                    // not the middle of one
    [InlineData("Bohemian Rhapsody", "rhapsody bohemian", true)]     // any order
    [InlineData("Bohemian Rhapsody", "bohemian like", false)]        // every word must be there
    [InlineData("Anything", "  ", false)]
    public void MatchesAllWords_is_word_prefix_and_order_free(string text, string query, bool expected)
        => Assert.Equal(expected, SearchText.MatchesAllWords(text, query));
}
