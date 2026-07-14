using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class KaraFunVenueUrlParserTests
{
    [Theory]
    [InlineData("https://www.karafun.com/076217/search?q=a+walk+through+hell")]
    [InlineData("https://www.karafun.com/076217/")]
    [InlineData("https://www.karafun.com/076217")]
    [InlineData("http://karafun.com/076217/search")]
    [InlineData("www.karafun.com/076217")]
    [InlineData("KARAFUN.COM/076217")]
    [InlineData("076217")]
    [InlineData("  076217  ")]
    public void TryParseId_extracts_venue_id_keeping_leading_zeros(string input)
    {
        Assert.True(KaraFunVenueUrlParser.TryParseId(input, out var id));
        Assert.Equal("076217", id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("hello world")]
    [InlineData("https://www.karafun.com/karaoke-say-anything")]   // a song page — no numeric venue segment
    [InlineData("https://open.spotify.com/playlist/076217")]        // right digits, wrong host
    public void TryParseId_rejects_non_venue_input(string? input)
    {
        Assert.False(KaraFunVenueUrlParser.TryParseId(input, out var id));
        Assert.Equal(string.Empty, id);
    }
}
