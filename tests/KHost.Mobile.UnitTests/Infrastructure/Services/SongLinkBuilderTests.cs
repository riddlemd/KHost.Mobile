using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// The three quick-link URL builders, all pure string logic. Grouped per destination, because the interesting
// differences are between them: YouTube joins with " - " while Spotify must not, and KaraFun carries the venue
// in the path rather than the query.
public class SongLinkBuilderTests
{
    private static readonly SongLinkBuilder Links = new();

    // ---- YouTube ----------------------------------------------------------------------------

    [Fact]
    public void YouTube_joins_title_and_artist_with_a_dash()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa%20-%20Toto",
            Links.YouTubeUrlFor("Africa", "Toto"));
    }

    [Fact]
    public void YouTube_uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            Links.YouTubeUrlFor("Africa", null));
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Africa",
            Links.YouTubeUrlFor("Africa", "   "));
    }

    [Fact]
    public void YouTube_trims_and_escapes_the_query()
    {
        // Surrounding whitespace trimmed; an inner space and an ampersand are percent-escaped.
        Assert.Equal(
            "https://www.youtube.com/results?search_query=Me%20%26%20You%20-%20The%20Band",
            Links.YouTubeUrlFor("  Me & You  ", "  The Band  "));
    }

    // ---- Spotify ----------------------------------------------------------------------------

    [Fact]
    public void Spotify_joins_title_and_artist_with_a_space_not_a_dash()
    {
        // A leading dash is Spotify's NOT operator, which would exclude the artist instead of searching for it.
        Assert.Equal(
            "https://open.spotify.com/search/Africa%20Toto",
            Links.SpotifyUrlFor("Africa", "Toto"));
    }

    [Fact]
    public void Spotify_uses_just_the_title_when_there_is_no_artist()
    {
        Assert.Equal(
            "https://open.spotify.com/search/Africa",
            Links.SpotifyUrlFor("Africa", null));
        Assert.Equal(
            "https://open.spotify.com/search/Africa",
            Links.SpotifyUrlFor("Africa", "   "));
    }

    [Fact]
    public void Spotify_trims_and_escapes_the_query()
    {
        Assert.Equal(
            "https://open.spotify.com/search/Me%20%26%20You%20The%20Band",
            Links.SpotifyUrlFor("  Me & You  ", "  The Band  "));
    }

    // ---- KaraFun ----------------------------------------------------------------------------

    [Fact]
    public void KaraFun_builds_venue_search_url_with_title_and_artist()
    {
        var url = Links.KaraFunUrlFor("012345", "A Walk Through Hell", "Say Anything");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_A%20Walk%20Through%20Hell%20Say%20Anything", url);
    }

    [Fact]
    public void KaraFun_omits_artist_when_blank()
    {
        var url = Links.KaraFunUrlFor("012345", "Bohemian Rhapsody", "");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_Bohemian%20Rhapsody", url);
    }

    [Fact]
    public void KaraFun_trims_pieces_and_encodes_reserved_characters()
    {
        var url = Links.KaraFunUrlFor(" 012345 ", "  Song & Co  ", "  A/B  ");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_Song%20%26%20Co%20A%2FB", url);
    }

    [Fact]
    public void KaraFun_catalog_url_is_the_venue_home_with_no_query()
    {
        Assert.Equal("https://www.karafun.com/012345/", Links.KaraFunCatalogUrlFor(" 012345 "));
    }
}
