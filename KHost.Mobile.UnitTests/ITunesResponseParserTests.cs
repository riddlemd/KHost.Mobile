using KHost.Mobile.Clients.Enrichment;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class ITunesResponseParserTests
{
    [Fact]
    public void Reads_year_genre_and_matched_names_on_a_clean_match()
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

        var meta = ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemical Romance");

        Assert.NotNull(meta);
        Assert.Equal("Helena", meta!.MatchedTitle);
        Assert.Equal("My Chemical Romance", meta.MatchedArtist);
        Assert.Equal(2004, meta.Year);
        Assert.Equal("Alternative", meta.Genre);
    }

    [Fact]
    public void Upscales_the_100x100_artwork_thumbnail_to_300x300()
    {
        const string json = """
        {
          "resultCount": 1,
          "results": [
            {
              "trackName": "Helena",
              "artistName": "My Chemical Romance",
              "releaseDate": "2004-06-08T07:00:00Z",
              "primaryGenreName": "Alternative",
              "artworkUrl100": "https://is1-ssl.mzstatic.com/image/thumb/abc/source/100x100bb.jpg"
            }
          ]
        }
        """;

        var meta = ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemical Romance");

        Assert.NotNull(meta);
        Assert.Equal("https://is1-ssl.mzstatic.com/image/thumb/abc/source/300x300bb.jpg", meta!.ArtworkUrl);
    }

    [Fact]
    public void Leaves_artwork_null_when_the_result_carries_none()
    {
        const string json = """
        {
          "resultCount": 1,
          "results": [
            { "trackName": "Helena", "artistName": "My Chemical Romance", "primaryGenreName": "Alternative" }
          ]
        }
        """;

        var meta = ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemical Romance");

        Assert.NotNull(meta);
        Assert.Null(meta!.ArtworkUrl);
    }

    [Fact]
    public void Rejects_a_right_title_wrong_artist_cover()
    {
        // The real "Wow, I Can Get Sexual Too" by Say Anything isn't in the catalog; iTunes returns covers
        // and unrelated songs. None share the artist, so nothing should be populated.
        const string json = """
        {
          "resultCount": 3,
          "results": [
            { "trackName": "Wow I Can Get Sexual Too", "artistName": "Sparrow Sleeps", "releaseDate": "2015-01-01T12:00:00Z", "primaryGenreName": "Children's Music" },
            { "trackName": "Wow, I Can Get Sexual Too", "artistName": "Michael Henry & Justin Robinett", "releaseDate": "2010-01-01T12:00:00Z", "primaryGenreName": "Singer/Songwriter" },
            { "trackName": "Wow, I Can Get Sexual Too", "artistName": "Hot Fuss", "releaseDate": "2025-04-10T12:00:00Z", "primaryGenreName": "EMO" }
          ]
        }
        """;

        Assert.Null(ITunesResponseParser.ParseBestMatch(json, "Wow, I Can Get Sexual Too", "Say Anything"));
    }

    [Fact]
    public void Matches_past_a_non_matching_top_result()
    {
        // The correct artist sits below a higher-ranked mismatch — we should still find it.
        const string json = """
        {
          "resultCount": 2,
          "results": [
            { "trackName": "Africa", "artistName": "Weezer", "releaseDate": "2019-01-24T12:00:00Z", "primaryGenreName": "Rock" },
            { "trackName": "Africa", "artistName": "TOTO", "releaseDate": "1982-04-08T12:00:00Z", "primaryGenreName": "Rock" }
          ]
        }
        """;

        var meta = ITunesResponseParser.ParseBestMatch(json, "Africa", "Toto");

        Assert.NotNull(meta);
        Assert.Equal("TOTO", meta!.MatchedArtist);
        Assert.Equal(1982, meta.Year);
    }

    [Theory]
    // Punctuation, case, and accents don't block a match.
    [InlineData("Wow I Can Get Sexual Too", "wow, i can get sexual too")]
    [InlineData("Beyoncé", "Beyonce")]
    // Feature / version qualifiers on the candidate are ignored.
    [InlineData("Helena", "Helena (Remastered 2011)")]
    [InlineData("So Cold", "So Cold (feat. Someone)")]
    [InlineData("Comfortably Numb", "Comfortably Numb - Live")]
    public void Normalization_accepts_the_same_song_written_differently(string requested, string candidateTitle)
    {
        var json = $$"""
        { "results": [ { "trackName": {{System.Text.Json.JsonSerializer.Serialize(candidateTitle)}}, "artistName": "The Band", "releaseDate": "1990-01-01T00:00:00Z", "primaryGenreName": "Rock" } ] }
        """;

        var meta = ITunesResponseParser.ParseBestMatch(json, requested, "The Band");

        Assert.NotNull(meta);
        Assert.Equal(1990, meta!.Year);
    }

    [Fact]
    public void Matches_when_optional_year_and_genre_are_missing()
    {
        const string json = """{ "results": [ { "trackName": "Mystery Song", "artistName": "The Band" } ] }""";

        var meta = ITunesResponseParser.ParseBestMatch(json, "Mystery Song", "The Band");

        Assert.NotNull(meta);
        Assert.Equal("Mystery Song", meta!.MatchedTitle);
        Assert.Null(meta.Year);
        Assert.Null(meta.Genre);
    }

    [Fact]
    public void Returns_null_when_the_artist_is_unknown()
    {
        // No artist to verify against → we won't guess.
        const string json = """{ "results": [ { "trackName": "Helena", "artistName": "My Chemical Romance", "primaryGenreName": "Alternative" } ] }""";

        Assert.Null(ITunesResponseParser.ParseBestMatch(json, "Helena", ""));
    }

    [Fact]
    public void Returns_null_on_zero_results()
    {
        Assert.Null(ITunesResponseParser.ParseBestMatch("""{ "resultCount": 0, "results": [] }""", "Helena", "My Chemical Romance"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ }")]
    public void Returns_null_on_unusable_payload(string json)
    {
        Assert.Null(ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemical Romance"));
    }
}
