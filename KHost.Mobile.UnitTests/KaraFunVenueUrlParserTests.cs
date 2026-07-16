using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class KaraFunVenueUrlParserTests
{
    [Theory]
    [InlineData("https://www.karafun.com/012345/search?q=a+walk+through+hell")]
    [InlineData("https://www.karafun.com/012345/")]
    [InlineData("https://www.karafun.com/012345")]
    [InlineData("http://karafun.com/012345/search")]
    [InlineData("www.karafun.com/012345")]
    [InlineData("KARAFUN.COM/012345")]
    [InlineData("012345")]
    [InlineData("  012345  ")]
    public void TryParseId_extracts_venue_id_keeping_leading_zeros(string input)
    {
        Assert.True(KaraFunVenueUrlParser.TryParseId(input, out var id));
        Assert.Equal("012345", id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("hello world")]
    [InlineData("https://www.karafun.com/karaoke-say-anything")]   // a song page — no numeric venue segment
    [InlineData("https://open.spotify.com/playlist/012345")]        // right digits, wrong host
    public void TryParseId_rejects_non_venue_input(string? input)
    {
        Assert.False(KaraFunVenueUrlParser.TryParseId(input, out var id));
        Assert.Equal(string.Empty, id);
    }

    [Theory]
    [InlineData("https://www.karafun.com/012345/search?q=a+walk+through+hell")]
    [InlineData("https://www.karafun.com/012345/")]
    [InlineData("https://www.karafun.com/012345")]
    [InlineData("http://karafun.com/012345/search")]
    [InlineData("HTTPS://WWW.KARAFUN.COM/012345")]   // host match is case-insensitive
    public void TryParseVenueUrl_extracts_id_from_a_valid_karafun_url(string input)
    {
        Assert.True(KaraFunVenueUrlParser.TryParseVenueUrl(input, out var id));
        Assert.Equal("012345", id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("012345")]                                       // a bare ID is not a URL — strict path rejects it
    [InlineData("www.karafun.com/012345")]                       // no scheme → not an absolute http(s) URL
    [InlineData("ftp://karafun.com/012345")]                     // wrong scheme
    [InlineData("https://evilkarafun.com/012345")]               // look-alike host
    [InlineData("https://karafun.com.evil.com/012345")]          // host suffix trick
    [InlineData("https://sub.karafun.com/012345")]               // an unlisted subdomain
    [InlineData("https://open.spotify.com/playlist/012345")]     // right digits, wrong host
    [InlineData("https://www.karafun.com/karaoke-say-anything")] // no numeric venue segment
    public void TryParseVenueUrl_rejects_non_karafun_or_malformed_urls(string? input)
    {
        Assert.False(KaraFunVenueUrlParser.TryParseVenueUrl(input, out var id));
        Assert.Equal(string.Empty, id);
    }
}
