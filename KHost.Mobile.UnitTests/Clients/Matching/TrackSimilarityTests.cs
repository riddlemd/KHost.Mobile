using KHost.Mobile.Clients.Matching;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Matching;

public class TrackSimilarityTests
{
    [Theory]
    [InlineData("bohemian rapsody", "bohemian rhapsody")]   // dropped letter
    [InlineData("helna", "helena")]                         // dropped letter, short word
    [InlineData("sweet caroline", "sweet karoline")]        // substitution
    [InlineData("dont stop beleivin", "dont stop believin")]// transposition reads as 2 edits
    [InlineData("nirvanna", "nirvana")]                     // doubled letter
    public void Reads_a_near_miss_as_a_typo(string candidate, string wanted)
        => Assert.NotNull(TrackSimilarity.TypoEdits(candidate, wanted));

    [Theory]
    [InlineData("africa", "america")]        // 2 edits, but too short to guess at
    [InlineData("yes", "yet")]               // 1 edit on a 3-letter word
    [InlineData("let it be", "let it go")]   // 2 edits, and two genuinely different songs
    [InlineData("toto", "kiss")]             // unrelated
    [InlineData("creep", "creeper")]         // a different title, not a slip
    public void Leaves_a_different_song_alone(string candidate, string wanted)
        => Assert.Null(TrackSimilarity.TypoEdits(candidate, wanted));

    [Fact]
    public void An_exact_string_is_a_match_not_a_correction()
        => Assert.Null(TrackSimilarity.TypoEdits("bohemian rhapsody", "bohemian rhapsody"));

    [Fact]
    public void Scores_a_closer_spelling_lower()
    {
        var oneEdit = TrackSimilarity.TypoEdits("bohemian rapsody", "bohemian rhapsody");
        var twoEdits = TrackSimilarity.TypoEdits("bohemin rapsody", "bohemian rhapsody");

        Assert.Equal(1, oneEdit);
        Assert.Equal(2, twoEdits);
    }

    [Fact]
    public void Gives_up_past_the_edit_ceiling()
    {
        Assert.Equal(2, TrackSimilarity.TypoEdits("bohemian rhapsodyyy", "bohemian rhapsody"));   // at the ceiling
        Assert.Null(TrackSimilarity.TypoEdits("bohemian rhapsodyyyy", "bohemian rhapsody"));      // one past it
    }

    [Fact]
    public void Handles_an_empty_side_without_throwing()
    {
        Assert.Null(TrackSimilarity.TypoEdits("", "helena"));
        Assert.Null(TrackSimilarity.TypoEdits("helena", ""));
    }

    [Fact]
    public void One_edit_needs_five_characters_to_earn_it()
    {
        Assert.Equal(1, TrackSimilarity.TypoEdits("creap", "creep"));   // 5 chars — exactly the bar
        Assert.Null(TrackSimilarity.TypoEdits("kass", "kiss"));         // 4 chars — one short
    }

    [Fact]
    public void Two_edits_need_ten_characters_to_earn_them()
    {
        Assert.Equal(2, TrackSimilarity.TypoEdits("wondorwoll", "wonderwall"));   // 10 chars — exactly the bar
        Assert.Null(TrackSimilarity.TypoEdits("lat it bo", "let it be"));         // 9 chars — one short
    }

    [Fact]
    public void A_big_length_gap_is_rejected_before_any_measuring()
    {
        Assert.Null(TrackSimilarity.TypoEdits("hey", "hey jude"));
    }

    [Theory]
    [InlineData("bohemian rapsody", "bohemian rhapsody")]
    [InlineData("africa", "america")]
    public void Reads_the_same_either_way_round(string a, string b)
        => Assert.Equal(TrackSimilarity.TypoEdits(a, b), TrackSimilarity.TypoEdits(b, a));

    [Fact]
    public void A_late_edit_in_a_long_string_still_lands()
    {
        // The row-by-row bail gives up once no cell can still come in under the ceiling. A long prefix that
        // matches perfectly keeps every early row at 0, so an edit right at the end must survive the scan.
        Assert.Equal(1, TrackSimilarity.TypoEdits("bohemian rhapsody live at wembly", "bohemian rhapsody live at wembley"));
    }
}
