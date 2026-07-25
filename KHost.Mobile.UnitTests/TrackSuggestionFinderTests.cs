using KHost.Mobile.Clients.Matching;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class TrackSuggestionFinderTests
{
    private const string WantTitle = "bohemian rhapsody";
    private const string WantArtist = "queen";

    // Simple normalized-equality stand-in for a source's own artist-variant rule.
    private static bool ArtistIsQueen(string? artist)
        => TrackTextNormalizer.Normalize(artist) == WantArtist;

    [Fact]
    public void Finds_a_title_anchored_typo_when_the_artist_is_exact()
    {
        var candidates = new (string? Title, string? Artist)[] { ("Bohemian Rapsody", "Queen") };   // title 1 edit off

        var suggestion = TrackSuggestionFinder.Best(candidates, WantTitle, WantArtist, ArtistIsQueen);

        Assert.NotNull(suggestion);
        Assert.Equal("Bohemian Rapsody", suggestion!.Title);
        Assert.Equal("Queen", suggestion.Artist);
    }

    [Fact]
    public void Finds_an_artist_anchored_typo_when_the_title_is_exact()
    {
        var candidates = new (string? Title, string? Artist)[] { ("Bohemian Rhapsody", "Qeen") };   // artist 1 edit off

        var suggestion = TrackSuggestionFinder.Best(candidates, WantTitle, WantArtist, ArtistIsQueen);

        Assert.NotNull(suggestion);
        Assert.Equal("Bohemian Rhapsody", suggestion!.Title);
        Assert.Equal("Qeen", suggestion.Artist);
    }

    [Fact]
    public void Returns_null_when_both_title_and_artist_are_exact()
    {
        var candidates = new (string? Title, string? Artist)[] { ("Bohemian Rhapsody", "Queen") };

        Assert.Null(TrackSuggestionFinder.Best(candidates, WantTitle, WantArtist, ArtistIsQueen));
    }

    [Fact]
    public void Returns_null_when_both_title_and_artist_are_wrong()
    {
        var candidates = new (string? Title, string? Artist)[] { ("Africa", "Toto") };

        Assert.Null(TrackSuggestionFinder.Best(candidates, WantTitle, WantArtist, ArtistIsQueen));
    }

    [Fact]
    public void The_closer_candidate_wins_regardless_of_order()
    {
        var far = ("Bohemian Rhapsodyyy", "Queen");    // 2 edits
        var close = ("Bohemian Rapsody", "Queen");      // 1 edit

        var farThenClose = TrackSuggestionFinder.Best([far, close], WantTitle, WantArtist, ArtistIsQueen);
        var closeThenFar = TrackSuggestionFinder.Best([close, far], WantTitle, WantArtist, ArtistIsQueen);

        Assert.Equal("Bohemian Rapsody", farThenClose!.Title);
        Assert.Equal("Bohemian Rapsody", closeThenFar!.Title);
    }

    [Fact]
    public void An_equal_edit_later_candidate_does_not_replace_the_earlier_one()
    {
        // Both exactly 1 edit from the wanted title, so a tie — first one in must stick.
        var first = ("Bohemian Rapsody", "Queen");
        var second = ("Bohemian Rhapsdy", "Queen");

        var suggestion = TrackSuggestionFinder.Best([first, second], WantTitle, WantArtist, ArtistIsQueen);

        Assert.Equal("Bohemian Rapsody", suggestion!.Title);
    }

    [Fact]
    public void The_offered_strings_are_stripped_of_asides()
    {
        var candidates = new (string? Title, string? Artist)[] { ("Creep (Acoustic)", "Radiohead") };

        var suggestion = TrackSuggestionFinder.Best(candidates, "creap", "radiohead", a => TrackTextNormalizer.Normalize(a) == "radiohead");

        Assert.NotNull(suggestion);
        Assert.Equal("Creep", suggestion!.Title);
        Assert.Equal("Radiohead", suggestion.Artist);
    }

    [Fact]
    public void Skips_a_candidate_with_a_blank_title_or_artist_without_throwing()
    {
        var candidates = new (string? Title, string? Artist)[]
        {
            (null, "Queen"),
            ("Bohemian Rhapsody", ""),
            ("   ", "Queen"),
        };

        var suggestion = TrackSuggestionFinder.Best(candidates, WantTitle, WantArtist, ArtistIsQueen);

        Assert.Null(suggestion);
    }
}
