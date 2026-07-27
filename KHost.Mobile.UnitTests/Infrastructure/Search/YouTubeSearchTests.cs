using KHost.Mobile.Search;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Search;

// The YouTube quick-link URL builder. Pure string logic. It joins title + artist with " - ";
// SpotifySearch deliberately doesn't — see SpotifySearchTests for why.
public class YouTubeSearchTests
{
    [Fact]
    public void Joins_title_and_artist_with_a_dash()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa%20-%20Toto",
            YouTubeSearch.UrlFor("Africa", "Toto"));
    }

    [Fact]
    public void Uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            YouTubeSearch.UrlFor("Africa", null));
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            YouTubeSearch.UrlFor("Africa", "   "));
    }

    [Fact]
    public void Trims_and_escapes_the_query()
    {
        // Surrounding whitespace trimmed; an inner space and an ampersand are percent-escaped.
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Me%20%26%20You%20-%20The%20Band",
            YouTubeSearch.UrlFor("  Me & You  ", "  The Band  "));
    }
}
