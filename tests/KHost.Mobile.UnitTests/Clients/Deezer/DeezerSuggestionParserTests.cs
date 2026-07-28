using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Deezer;

/// <summary>
/// Deezer's spelling-correction fallback, reached only when iTunes offered neither a match nor a correction.
/// Same bar as the iTunes path — one field spelled exactly right, the other within a couple of edits.
/// </summary>
public class DeezerSuggestionParserTests
{
    private static string Results(params (string Title, string Artist)[] rows)
        => $$"""
        {
          "data": [
            {{string.Join(",\n    ", rows.Select(r =>
                $$"""{ "title": "{{r.Title}}", "artist": { "name": "{{r.Artist}}" } }"""))}}
          ]
        }
        """;

    [Fact]
    public void Offers_the_catalogue_spelling_when_only_the_title_is_off()
    {
        var json = Results(("Creep", "Radiohead"));

        var suggestion = DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead");

        Assert.Equal("Creep", suggestion!.Title);
        Assert.Equal("Radiohead", suggestion.Artist);
    }

    [Fact]
    public void Does_not_hand_a_cover_artist_back_as_the_correction()
    {
        // Deezer's free-text search really does return this alongside the real one — the artist anchor is what
        // keeps "Creep" by the Glee Cast from being offered as the fix for a Radiohead entry.
        var json = Results(("Creep (Cover of Radiohead)", "Glee Cast"), ("Creep", "Radiohead"));

        var suggestion = DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead");

        Assert.Equal("Radiohead", suggestion!.Artist);
    }

    [Fact]
    public void Treats_a_band_name_variant_as_the_anchor_not_a_typo()
    {
        // "The White Stripes" vs "White Stripes" is a variant Deezer's artist matcher already accepts, so it
        // anchors the comparison — the title is what gets corrected, and the artist is never "fixed".
        var json = Results(("Seven Nation Army", "The White Stripes"));

        var suggestion = DeezerSuggestionParser.ParseSuggestion(json, "Seven Nation Armee", "White Stripes");

        Assert.Equal("Seven Nation Army", suggestion!.Title);
    }

    [Fact]
    public void Says_nothing_when_both_sides_are_off()
    {
        var json = Results(("Creep", "Radiohead"));

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohedd"));
    }

    [Fact]
    public void Says_nothing_when_the_results_are_a_different_song()
    {
        var json = Results(("Karma Police", "Radiohead"), ("No Surprises", "Radiohead"));

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead"));
    }

    [Fact]
    public void An_exact_match_is_never_reported_as_a_correction()
    {
        var json = Results(("Creep", "Radiohead"));

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creep", "Radiohead"));
    }

    [Fact]
    public void Stays_quiet_when_the_catalogue_itself_carries_the_typo()
    {
        // Real case: Deezer's top hit for "Jacques Brel Ne Me Quite Pas" is a track titled with the misspelling.
        // Both sides then "match", so this reports nothing rather than confirming the typo back at the user —
        // and iTunes, asked first, returns the correct "Ne me quitte pas" anyway.
        var json = Results(("Ne Me Quite Pas", "Jacques Brel"));

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Ne Me Quite Pas", "Jacques Brel"));
    }

    [Fact]
    public void Strips_a_version_suffix_from_the_offer()
    {
        var json = Results(("Helena (Live)", "My Chemical Romance"));

        var suggestion = DeezerSuggestionParser.ParseSuggestion(json, "Helna", "My Chemical Romance");

        Assert.Equal("Helena", suggestion!.Title);
    }

    [Fact]
    public void Treats_a_Deezer_error_body_as_no_suggestion()
    {
        // Deezer reports quota faults as a 200 with an "error" object rather than an HTTP status.
        const string json = """{ "error": { "type": "Exception", "message": "Quota limit exceeded", "code": 4 } }""";

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""{ "data": "unexpected" }""")]
    public void Survives_an_unusable_payload(string json)
        => Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead"));

    /// <summary>
    /// A real (trimmed) Deezer response for the free-text query "Radiohead Creap". Pinned verbatim because it
    /// carries three separate traps: a same-titled cover by another artist, a version suffix, and a title that
    /// is nothing BUT a parenthetical.
    /// </summary>
    private const string CreapResponse = """
    {
      "data": [
        { "title": "Creep", "artist": { "name": "Radiohead" } },
        { "title": "(Nice Dream)", "artist": { "name": "Radiohead" } },
        { "title": "Creep (Cover of Radiohead)", "artist": { "name": "Glee Cast" } },
        { "title": "Blow Out (Remix)", "artist": { "name": "Radiohead" } },
        { "title": "Inside My Head", "artist": { "name": "Radiohead" } },
        { "title": "Creep (Acoustic)", "artist": { "name": "Radiohead" } }
      ]
    }
    """;

    [Fact]
    public void Picks_the_clean_title_out_of_a_real_response()
    {
        var suggestion = DeezerSuggestionParser.ParseSuggestion(CreapResponse, "Creap", "Radiohead");

        // "Creep (Acoustic)" is an equally close match further down; the first-wins tie-break keeps the plain one.
        Assert.Equal("Creep", suggestion!.Title);
        Assert.Equal("Radiohead", suggestion.Artist);
    }

    [Fact]
    public void A_wholly_parenthetical_title_does_not_blow_up_the_comparison()
    {
        // "(Nice Dream)" normalizes to an empty string. It must be skipped, not treated as a zero-length near-miss.
        var json = Results(("(Nice Dream)", "Radiohead"));

        Assert.Null(DeezerSuggestionParser.ParseSuggestion(json, "Creap", "Radiohead"));
    }
}
