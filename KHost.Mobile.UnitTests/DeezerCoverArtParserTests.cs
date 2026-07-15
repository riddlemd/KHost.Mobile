using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class DeezerCoverArtParserTests
{
    [Fact]
    public void Returns_the_album_cover_on_a_clean_match()
    {
        const string json = """
        {
          "data": [
            {
              "title": "Aeroplane",
              "artist": { "name": "Red Hot Chili Peppers" },
              "album": {
                "title": "One Hot Minute",
                "cover_medium": "https://cdn/250.jpg",
                "cover_big": "https://cdn/500.jpg",
                "cover_xl": "https://cdn/1000.jpg"
              }
            }
          ]
        }
        """;

        var url = DeezerCoverArtParser.ParseCoverArtUrl(json, "Aeroplane", "Red Hot Chili Peppers");

        Assert.Equal("https://cdn/500.jpg", url);   // prefers cover_big
    }

    [Fact]
    public void Falls_back_to_a_smaller_cover_when_big_is_absent()
    {
        const string json = """
        { "data": [ { "title": "Song", "artist": { "name": "The Band" }, "album": { "cover_medium": "https://cdn/250.jpg" } } ] }
        """;

        Assert.Equal("https://cdn/250.jpg", DeezerCoverArtParser.ParseCoverArtUrl(json, "Song", "The Band"));
    }

    [Fact]
    public void Rejects_a_right_title_wrong_artist_cover()
    {
        // "Country song" resolved to a totally different artist in live testing — must not return its cover.
        const string json = """
        { "data": [ { "title": "Country Song", "artist": { "name": "Bo Hazard" }, "album": { "cover_big": "https://cdn/x.jpg" } } ] }
        """;

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Country song", "Bo Burnham"));
    }

    [Theory]
    // Deezer's artist name is often a superset/subset of ours — these should all still match.
    [InlineData("White Stripes", "The White Stripes")]
    [InlineData("Hall & Oates", "Daryl Hall & John Oates")]
    [InlineData("Ben Folds", "Ben Folds Five")]
    [InlineData("Giovannie & The Hired Guns", "Giovannie and the Hired Guns")]
    public void Accepts_artist_name_variants(string requested, string deezerArtist)
    {
        var json = $$"""
        { "data": [ { "title": "Song", "artist": { "name": {{System.Text.Json.JsonSerializer.Serialize(deezerArtist)}} }, "album": { "cover_big": "https://cdn/ok.jpg" } } ] }
        """;

        Assert.Equal("https://cdn/ok.jpg", DeezerCoverArtParser.ParseCoverArtUrl(json, "Song", requested));
    }

    [Fact]
    public void Rejects_a_single_token_artist_that_is_only_a_prefix_of_another()
    {
        // "Prince" is a subset of "Prince Royce", but a one-token overlap must not match a different artist.
        const string json = """
        { "data": [ { "title": "Kiss", "artist": { "name": "Prince Royce" }, "album": { "cover_big": "https://cdn/x.jpg" } } ] }
        """;

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Kiss", "Prince"));
    }

    [Fact]
    public void Matches_past_a_non_matching_top_result()
    {
        const string json = """
        {
          "data": [
            { "title": "Africa", "artist": { "name": "Weezer" }, "album": { "cover_big": "https://cdn/weezer.jpg" } },
            { "title": "Africa", "artist": { "name": "TOTO" }, "album": { "cover_big": "https://cdn/toto.jpg" } }
          ]
        }
        """;

        Assert.Equal("https://cdn/toto.jpg", DeezerCoverArtParser.ParseCoverArtUrl(json, "Africa", "Toto"));
    }

    [Theory]
    [InlineData("Aeroplane", "Aeroplane")]
    [InlineData("Sweet Dreams (Are Made of This)", "Sweet Dreams")]
    [InlineData("Beyoncé", "Beyonce")]
    public void Title_normalization_accepts_the_same_song_written_differently(string deezerTitle, string requested)
    {
        var json = $$"""
        { "data": [ { "title": {{System.Text.Json.JsonSerializer.Serialize(deezerTitle)}}, "artist": { "name": "The Band" }, "album": { "cover_big": "https://cdn/ok.jpg" } } ] }
        """;

        Assert.Equal("https://cdn/ok.jpg", DeezerCoverArtParser.ParseCoverArtUrl(json, requested, "The Band"));
    }

    [Fact]
    public void Returns_null_when_the_title_does_not_match()
    {
        const string json = """
        { "data": [ { "title": "A Different Song", "artist": { "name": "The Band" }, "album": { "cover_big": "https://cdn/x.jpg" } } ] }
        """;

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Wanted Song", "The Band"));
    }

    [Fact]
    public void Returns_null_when_the_match_carries_no_cover()
    {
        const string json = """
        { "data": [ { "title": "Song", "artist": { "name": "The Band" }, "album": { "title": "Album" } } ] }
        """;

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Song", "The Band"));
    }

    [Fact]
    public void Treats_a_deezer_error_object_as_no_cover()
    {
        // Deezer returns quota (code 4) and other faults as a 200 body with an error object.
        const string json = """{ "error": { "type": "Exception", "message": "Quota limit exceeded", "code": 4 } }""";

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Aeroplane", "Red Hot Chili Peppers"));
    }

    [Fact]
    public void Returns_null_when_the_artist_is_blank()
    {
        const string json = """{ "data": [ { "title": "Song", "artist": { "name": "The Band" }, "album": { "cover_big": "https://cdn/x.jpg" } } ] }""";

        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Song", ""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ }")]
    [InlineData("""{ "data": [] }""")]
    public void Returns_null_on_unusable_payload(string json)
    {
        Assert.Null(DeezerCoverArtParser.ParseCoverArtUrl(json, "Song", "The Band"));
    }
}
