using KHost.Mobile.Clients.Lyrics;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class LrcLibResponseParserTests
{
    [Fact]
    public void ParseFirst_prefers_the_first_record_that_carries_lyrics()
    {
        // A bare metadata-only hit is first in the array; the record with lyrics is second and should win.
        const string json = """
        [
          { "trackName": "Bohemian Rhapsody", "artistName": "Queen", "plainLyrics": null, "syncedLyrics": null, "instrumental": false },
          { "trackName": "Bohemian Rhapsody", "artistName": "Queen", "plainLyrics": "Is this the real life?", "syncedLyrics": "[00:00.00] Is this the real life?", "instrumental": false }
        ]
        """;

        var result = LrcLibResponseParser.ParseFirst(json);

        Assert.NotNull(result);
        Assert.Equal("Bohemian Rhapsody", result!.MatchedTitle);
        Assert.Equal("Queen", result.MatchedArtist);
        Assert.Equal("Is this the real life?", result.PlainLyrics);
        Assert.Equal("[00:00.00] Is this the real life?", result.SyncedLyrics);
        Assert.False(result.Instrumental);
    }

    [Fact]
    public void ParseFirst_accepts_an_instrumental_record_even_without_lyrics()
    {
        const string json = """
        [ { "trackName": "Jessica", "artistName": "The Allman Brothers Band", "plainLyrics": "", "instrumental": true } ]
        """;

        var result = LrcLibResponseParser.ParseFirst(json);

        Assert.NotNull(result);
        Assert.True(result!.Instrumental);
    }

    [Fact]
    public void ParseFirst_falls_back_to_the_first_record_when_none_carry_lyrics()
    {
        // Neither record has lyrics nor is instrumental → the first one is surfaced (UI shows "no lyrics").
        const string json = """
        [
          { "trackName": "First Hit", "artistName": "A", "plainLyrics": "   ", "instrumental": false },
          { "trackName": "Second Hit", "artistName": "B", "plainLyrics": null, "instrumental": false }
        ]
        """;

        var result = LrcLibResponseParser.ParseFirst(json);

        Assert.NotNull(result);
        Assert.Equal("First Hit", result!.MatchedTitle);   // the first record, verbatim — including its blank lyrics
    }

    [Fact]
    public void ParseFirst_treats_whitespace_only_lyrics_as_not_carrying_lyrics()
    {
        // The first record's lyrics are whitespace (doesn't count); the second has real lyrics and should win.
        const string json = """
        [
          { "trackName": "Blank", "artistName": "A", "plainLyrics": "   " },
          { "trackName": "Real", "artistName": "B", "plainLyrics": "Actual words here" }
        ]
        """;

        var result = LrcLibResponseParser.ParseFirst(json);

        Assert.NotNull(result);
        Assert.Equal("Real", result!.MatchedTitle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ \"trackName\": \"Not An Array\" }")]   // object, not the expected array
    [InlineData("[]")]                                      // empty array
    public void ParseFirst_returns_null_on_unusable_payload(string json)
    {
        Assert.Null(LrcLibResponseParser.ParseFirst(json));
    }
}
