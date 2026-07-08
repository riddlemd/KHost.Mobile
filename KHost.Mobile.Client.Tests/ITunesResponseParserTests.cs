using KHost.Mobile.Client.Enrichment;
using Xunit;

namespace KHost.Mobile.Client.Tests;

public class ITunesResponseParserTests
{
    [Fact]
    public void ParseFirst_reads_year_genre_and_matched_names()
    {
        const string json = """
        {
          "resultCount": 1,
          "results": [
            {
              "trackName": "Helena",
              "artistName": "My Chemical Romance",
              "collectionName": "Three Cheers for Sweet Revenge",
              "releaseDate": "2004-06-08T07:00:00Z",
              "primaryGenreName": "Alternative"
            }
          ]
        }
        """;

        var meta = ITunesResponseParser.ParseFirst(json);

        Assert.NotNull(meta);
        Assert.Equal("Helena", meta!.MatchedTitle);
        Assert.Equal("My Chemical Romance", meta.MatchedArtist);
        Assert.Equal(2004, meta.Year);
        Assert.Equal("Alternative", meta.Genre);
    }

    [Fact]
    public void ParseFirst_returns_null_on_zero_results()
    {
        Assert.Null(ITunesResponseParser.ParseFirst("""{ "resultCount": 0, "results": [] }"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ }")]
    public void ParseFirst_returns_null_on_unusable_payload(string json)
    {
        Assert.Null(ITunesResponseParser.ParseFirst(json));
    }

    [Fact]
    public void ParseFirst_tolerates_missing_fields()
    {
        // Only a track name; no releaseDate/genre. Should still return a result with nulls, not throw.
        const string json = """{ "results": [ { "trackName": "Mystery Song" } ] }""";

        var meta = ITunesResponseParser.ParseFirst(json);

        Assert.NotNull(meta);
        Assert.Equal("Mystery Song", meta!.MatchedTitle);
        Assert.Null(meta.Year);
        Assert.Null(meta.Genre);
    }
}
