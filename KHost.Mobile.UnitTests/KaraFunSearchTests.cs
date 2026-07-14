using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class KaraFunSearchTests
{
    [Fact]
    public void UrlFor_builds_venue_search_url_with_title_and_artist()
    {
        var url = KaraFunSearch.UrlFor("076217", "A Walk Through Hell", "Say Anything");
        Assert.Equal("https://www.karafun.com/076217/search?q=A%20Walk%20Through%20Hell%20Say%20Anything", url);
    }

    [Fact]
    public void UrlFor_omits_artist_when_blank()
    {
        var url = KaraFunSearch.UrlFor("076217", "Bohemian Rhapsody", "");
        Assert.Equal("https://www.karafun.com/076217/search?q=Bohemian%20Rhapsody", url);
    }

    [Fact]
    public void UrlFor_trims_pieces_and_encodes_reserved_characters()
    {
        var url = KaraFunSearch.UrlFor(" 076217 ", "  Song & Co  ", "  A/B  ");
        Assert.Equal("https://www.karafun.com/076217/search?q=Song%20%26%20Co%20A%2FB", url);
    }
}
