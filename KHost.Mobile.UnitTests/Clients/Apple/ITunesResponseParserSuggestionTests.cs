using KHost.Mobile.Clients.Apple;
using KHost.Mobile.Clients.Metadata;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Apple;

/// <summary>
/// The "did you mean …?" path: what the parser offers when nothing matched outright. The bar is deliberately
/// high — a false suggestion on a correctly-spelled song is worse than no suggestion at all.
/// </summary>
public class ITunesResponseParserSuggestionTests
{
    private static string Results(params (string Title, string Artist)[] rows)
        => $$"""
        {
          "resultCount": {{rows.Length}},
          "results": [
            {{string.Join(",\n    ", rows.Select(r =>
                $$"""{ "trackName": "{{r.Title}}", "artistName": "{{r.Artist}}", "primaryGenreName": "Rock", "releaseDate": "1975-10-31T07:00:00Z" }"""))}}
          ]
        }
        """;

    [Fact]
    public void Offers_the_catalogue_spelling_when_only_the_title_is_off()
    {
        var json = Results(("Bohemian Rhapsody", "Queen"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Bohemian Rapsody", "Queen");

        Assert.Null(result.Match);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("Bohemian Rhapsody", result.Suggestion!.Title);
        Assert.Equal("Queen", result.Suggestion.Artist);
    }

    [Fact]
    public void Offers_the_catalogue_spelling_when_only_the_artist_is_off()
    {
        var json = Results(("Helena", "My Chemical Romance"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemcial Romance");

        Assert.NotNull(result.Suggestion);
        Assert.Equal("My Chemical Romance", result.Suggestion!.Artist);
    }

    [Fact]
    public void Says_nothing_when_both_sides_are_off()
    {
        // No anchor: with neither field spelled right this is far more likely a different song than two typos.
        var json = Results(("Bohemian Rhapsody", "Queen"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Bohemian Rapsody", "Quean");

        Assert.Null(result.Match);
        Assert.Null(result.Suggestion);
    }

    [Fact]
    public void Says_nothing_when_the_song_simply_is_not_in_the_catalogue()
    {
        var json = Results(("Africa", "Toto"), ("Rosanna", "Toto"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Karaoke Night Original", "Toto");

        Assert.Null(result.Suggestion);
    }

    [Fact]
    public void Prefers_the_closest_spelling_over_a_farther_one()
    {
        // Both are within the ceiling against "Helna"; the single-edit one should win.
        var json = Results(("Helenna", "My Chemical Romance"), ("Helena", "My Chemical Romance"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Helna", "My Chemical Romance");

        Assert.Equal("Helena", result.Suggestion!.Title);
    }

    [Fact]
    public void An_exact_match_is_never_reported_as_a_correction()
    {
        var json = Results(("Helena", "My Chemical Romance"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Helena", "My Chemical Romance");

        Assert.NotNull(result.Match);
        Assert.Null(result.Suggestion);
    }

    [Fact]
    public void A_formatting_difference_matches_rather_than_suggesting()
    {
        // The normalizer already folds these, so they must not surface as a spelling correction.
        var json = Results(("Wow, I Can Get Sexual Too", "Say Anything"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Wow I Can Get Sexual Too", "Say Anything");

        Assert.NotNull(result.Match);
        Assert.Null(result.Suggestion);
    }

    [Fact]
    public void Says_nothing_without_an_artist_to_anchor_against()
    {
        var json = Results(("Helena", "My Chemical Romance"));

        var result = ITunesResponseParser.ParseBestMatch(json, "Helna", "");

        Assert.Null(result.Match);
        Assert.Null(result.Suggestion);
    }

    /// <summary>
    /// A real (trimmed) response for the query "Radiohead Creap". Worth pinning verbatim: the correct song only
    /// appears as "Creep (Acoustic)", and three of the five other rows are exact-titled covers by other artists.
    /// </summary>
    private const string CreapResponse = """
    {
      "resultCount": 6,
      "results": [
        { "trackName": "Creep (Acoustic)", "artistName": "Radiohead", "primaryGenreName": "Alternative", "releaseDate": "1992-09-21T12:00:00Z" },
        { "trackName": "Creep (Radiohead Cover)", "artistName": "Sunfly House Band", "primaryGenreName": "Pop", "releaseDate": "2021-02-06T12:00:00Z" },
        { "trackName": "Creep", "artistName": "Stone Temple Pilots", "primaryGenreName": "Pop", "releaseDate": "1992-09-29T12:00:00Z" },
        { "trackName": "Creep (feat. Radiohead) [Very 2021 Rmx]", "artistName": "Thom Yorke", "primaryGenreName": "Alternative", "releaseDate": "2021-07-13T12:00:00Z" },
        { "trackName": "Creep (Radiohead Cover)", "artistName": "Daniel Chuer", "primaryGenreName": "R&B/Soul", "releaseDate": "2023-02-01T12:00:00Z" },
        { "trackName": "No Surprises", "artistName": "Radiohead", "primaryGenreName": "Alternative", "releaseDate": "1997-05-21T07:00:00Z" }
      ]
    }
    """;

    [Fact]
    public void Suggests_the_plain_song_name_from_a_real_response()
    {
        var result = ITunesResponseParser.ParseBestMatch(CreapResponse, "Creap", "Radiohead");

        Assert.Null(result.Match);
        // "(Acoustic)" is a version of the same song, not part of what the user meant to type.
        Assert.Equal("Creep", result.Suggestion!.Title);
        Assert.Equal("Radiohead", result.Suggestion.Artist);
    }

    [Fact]
    public void Does_not_hand_a_cover_artist_back_as_the_correction()
    {
        // "Creep" by Stone Temple Pilots is spelled exactly right — but it's a different artist, so with the
        // artist as the anchor it can never be offered as a fix for a Radiohead entry.
        var result = ITunesResponseParser.ParseBestMatch(CreapResponse, "Creap", "Radiohead");

        Assert.NotEqual("Stone Temple Pilots", result.Suggestion!.Artist);
    }

    [Fact]
    public void Survives_an_empty_result_set()
    {
        var result = ITunesResponseParser.ParseBestMatch("""{ "resultCount": 0, "results": [] }""", "Helna", "Queen");

        Assert.Null(result.Match);
        Assert.Null(result.Suggestion);
    }
}
