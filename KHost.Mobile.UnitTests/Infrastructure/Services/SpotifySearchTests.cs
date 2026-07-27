using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// The Spotify quick-link URL builder. Pure string logic. Unlike YouTubeSearch it joins title + artist with a
// plain space, never " - ", because a leading dash is Spotify's NOT operator and would exclude the artist.
public class SpotifySearchTests
{
    [Fact]
    public void Joins_title_and_artist_with_a_space_not_a_dash()
    {
        Assert.Equal(
            "https://open.spotify.com/search/Africa%20Toto",
            SpotifySearch.UrlFor("Africa", "Toto"));
    }

    [Fact]
    public void Uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://open.spotify.com/search/Africa",
            SpotifySearch.UrlFor("Africa", null));
        Assert.Equal(
            "https://open.spotify.com/search/Africa",
            SpotifySearch.UrlFor("Africa", "   "));
    }

    [Fact]
    public void Trims_and_escapes_the_query()
    {
        // Surrounding whitespace trimmed; an inner space and an ampersand are percent-escaped.
        Assert.Equal(
            "https://open.spotify.com/search/Me%20%26%20You%20The%20Band",
            SpotifySearch.UrlFor("  Me & You  ", "  The Band  "));
    }
}
