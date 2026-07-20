using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The YouTube / Spotify quick-link URL builders. Pure string logic; the difference that matters is how each
// joins title + artist (YouTube uses " - ", Spotify a plain space to dodge its leading-dash NOT operator).
public class SongLinkSearchTests
{
    [Fact]
    public void YouTube_joins_title_and_artist_with_a_dash()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa%20-%20Toto",
            YouTubeSearch.UrlFor("Africa", "Toto"));
    }

    [Fact]
    public void YouTube_uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            YouTubeSearch.UrlFor("Africa", null));
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            YouTubeSearch.UrlFor("Africa", "   "));
    }

    [Fact]
    public void Spotify_joins_title_and_artist_with_a_space_not_a_dash()
    {
        // A leading dash is Spotify's NOT operator, so the artist must not be dash-prefixed.
        Assert.Equal(
            "https://open.spotify.com/search/Africa%20Toto",
            SpotifySearch.UrlFor("Africa", "Toto"));
    }

    [Fact]
    public void Spotify_uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://open.spotify.com/search/Africa",
            SpotifySearch.UrlFor("Africa", null));
    }

    [Fact]
    public void Both_trim_and_escape_the_query()
    {
        // Surrounding whitespace trimmed; an inner space and an ampersand are percent-escaped.
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Me%20%26%20You%20-%20The%20Band",
            YouTubeSearch.UrlFor("  Me & You  ", "  The Band  "));
        Assert.Equal(
            "https://open.spotify.com/search/Me%20%26%20You%20The%20Band",
            SpotifySearch.UrlFor("  Me & You  ", "  The Band  "));
    }
}
