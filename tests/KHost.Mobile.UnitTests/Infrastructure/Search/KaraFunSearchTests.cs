using KHost.Mobile.Infrastructure.Logic;
using KHost.Mobile.Infrastructure.Search;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Search;

public class KaraFunSearchTests
{
    private static readonly SongLinks Links = new();

    [Fact]
    public void UrlFor_builds_venue_search_url_with_title_and_artist()
    {
        var url = Links.KaraFunUrlFor("012345", "A Walk Through Hell", "Say Anything");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_A%20Walk%20Through%20Hell%20Say%20Anything", url);
    }

    [Fact]
    public void UrlFor_omits_artist_when_blank()
    {
        var url = Links.KaraFunUrlFor("012345", "Bohemian Rhapsody", "");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_Bohemian%20Rhapsody", url);
    }

    [Fact]
    public void UrlFor_trims_pieces_and_encodes_reserved_characters()
    {
        var url = Links.KaraFunUrlFor(" 012345 ", "  Song & Co  ", "  A/B  ");
        Assert.Equal("https://www.karafun.com/012345/search?q=sc_Song%20%26%20Co%20A%2FB", url);
    }

    [Fact]
    public void CatalogUrlFor_builds_the_venue_home_with_no_query()
    {
        Assert.Equal("https://www.karafun.com/012345/", Links.KaraFunCatalogUrlFor(" 012345 "));
    }
}
