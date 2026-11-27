using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Deezer;

// The fixtures are real Deezer replies (captured 2026-11-27, cover URLs replaced) for what a singer actually types,
// so the tidy-up rules are tested against the mess the API really returns, not an idealised one.
public class DeezerSongSearchParserTests
{
    // "dont stop belie": Journey's original, a 2024 remaster, a re-recording and a compilation copy, plus a Glee
    // cover and an anonymous "80's Greatest Hits" knock-off.
    private const string DontStopBelie = @"{""data"": [{""title"": ""Don't Stop Believin'"", ""artist"": {""name"": ""Journey""}, ""album"": {""title"": ""The Essential Journey"", ""cover_medium"": ""https://cdn.test/0.jpg""}}, {""title"": ""Don't Stop Believin' (2024 Remaster)"", ""artist"": {""name"": ""Journey""}, ""album"": {""title"": ""Greatest Hits (2024 Remaster)"", ""cover_medium"": ""https://cdn.test/1.jpg""}}, {""title"": ""Don't Stop Believin'"", ""artist"": {""name"": ""Teddy Swims""}, ""album"": {""title"": ""Don't Stop Believin'"", ""cover_medium"": ""https://cdn.test/2.jpg""}}, {""title"": ""Don't Stop Believin' (Cover of Journey)"", ""artist"": {""name"": ""Glee Cast""}, ""album"": {""title"": ""Glee: The Music, Volume 1"", ""cover_medium"": ""https://cdn.test/3.jpg""}}, {""title"": ""Don't Stop Believin'"", ""artist"": {""name"": ""Love Canon""}, ""album"": {""title"": ""Greatest Hits Vol. 2"", ""cover_medium"": ""https://cdn.test/4.jpg""}}, {""title"": ""Don't Stop Believin' (Re-Recorded)"", ""artist"": {""name"": ""Journey""}, ""album"": {""title"": ""Revelation"", ""cover_medium"": ""https://cdn.test/5.jpg""}}, {""title"": ""Don't Stop Believin'"", ""artist"": {""name"": ""Journey""}, ""album"": {""title"": ""80s 100 Hits"", ""cover_medium"": ""https://cdn.test/6.jpg""}}, {""title"": ""Don't Stop Believing"", ""artist"": {""name"": ""80's Greatest Hits""}, ""album"": {""title"": ""The 80's Greatest Hits: Pop Music (Classics)"", ""cover_medium"": ""https://cdn.test/7.jpg""}}]}";

    // "valerie amy": eight Amy Winehouse live versions and the Mark Ronson single she features on.
    private const string ValerieAmy = @"{""data"": [{""title"": ""Valerie (Live At BBC Radio 1 Live Lounge, London / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""Back To Black (Deluxe Edition)"", ""cover_medium"": ""https://cdn.test/0.jpg""}}, {""title"": ""Valerie (feat. Amy Winehouse) (Version Revisited)"", ""artist"": {""name"": ""Mark Ronson""}, ""album"": {""title"": ""Version"", ""cover_medium"": ""https://cdn.test/1.jpg""}}, {""title"": ""Valerie (BBC Radio 1 Live Lounge)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""AMY (Original Motion Picture Soundtrack)"", ""cover_medium"": ""https://cdn.test/2.jpg""}}, {""title"": ""Valerie (Live Jo Whiley, BBC Live Lounge Session / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""At The BBC"", ""cover_medium"": ""https://cdn.test/3.jpg""}}, {""title"": ""Valerie (Live From Shepherd’s Bush Empire, London / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""I Told You I Was Trouble: Live In London"", ""cover_medium"": ""https://cdn.test/4.jpg""}}, {""title"": ""Valerie (Live At BBC Radio 1 Live Lounge, London, UK / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""Back To Black (20th Anniversary Edition)"", ""cover_medium"": ""https://cdn.test/5.jpg""}}, {""title"": ""Valerie (Live At Porchester Hall / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""At The BBC"", ""cover_medium"": ""https://cdn.test/6.jpg""}}, {""title"": ""Valerie (Live At Paradiso, Amsterdam / 2007)"", ""artist"": {""name"": ""Amy Winehouse""}, ""album"": {""title"": ""Back To Black (20th Anniversary Edition)"", ""cover_medium"": ""https://cdn.test/7.jpg""}}]}";

    // "brightside": two Killers releases, then six different songs that share the word.
    private const string Brightside = @"{""data"": [{""title"": ""Mr. Brightside"", ""artist"": {""name"": ""The Killers""}, ""album"": {""title"": ""Direct Hits"", ""cover_medium"": ""https://cdn.test/0.jpg""}}, {""title"": ""Mr. Brightside"", ""artist"": {""name"": ""The Killers""}, ""album"": {""title"": ""Hot Fuss"", ""cover_medium"": ""https://cdn.test/1.jpg""}}, {""title"": ""The Brightside"", ""artist"": {""name"": ""Lil Peep""}, ""album"": {""title"": ""The Brightside"", ""cover_medium"": ""https://cdn.test/2.jpg""}}, {""title"": ""Brightside"", ""artist"": {""name"": ""Arrested Youth""}, ""album"": {""title"": ""Brightside"", ""cover_medium"": ""https://cdn.test/3.jpg""}}, {""title"": ""BRIGHTSIDE"", ""artist"": {""name"": ""The Lumineers""}, ""album"": {""title"": ""BRIGHTSIDE"", ""cover_medium"": ""https://cdn.test/4.jpg""}}, {""title"": ""Brightside"", ""artist"": {""name"": ""Halestorm""}, ""album"": {""title"": ""Back From the Dead"", ""cover_medium"": ""https://cdn.test/5.jpg""}}, {""title"": ""brightside"", ""artist"": {""name"": ""Drod""}, ""album"": {""title"": ""brightside"", ""cover_medium"": ""https://cdn.test/6.jpg""}}, {""title"": ""Bright Side"", ""artist"": {""name"": ""Mozambo""}, ""album"": {""title"": ""Bright Side"", ""cover_medium"": ""https://cdn.test/7.jpg""}}]}";

    [Fact]
    public void Folds_every_version_of_a_song_into_one_hit_that_counts_the_rest()
    {
        var hits = DeezerSongSearchParser.Parse(DontStopBelie, 6)!;

        var journey = Assert.Single(hits, h => h.Artist == "Journey");
        Assert.Equal("Don't Stop Believin'", journey.Title);
        Assert.Equal(3, journey.OtherVersions);
    }

    [Fact]
    public void Drops_covers_and_karaoke_compilation_artists()
    {
        var hits = DeezerSongSearchParser.Parse(DontStopBelie, 6)!;

        Assert.DoesNotContain(hits, h => h.Artist.Contains("Glee"));
        Assert.DoesNotContain(hits, h => h.Artist.Contains("Greatest Hits"));
        Assert.DoesNotContain(hits, h => h.Title.Contains("Cover", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Keeps_a_different_artists_recording_of_the_same_title_as_its_own_hit()
    {
        // Teddy Swims' version isn't labelled a cover — it's a separate recording someone may well want.
        var hits = DeezerSongSearchParser.Parse(DontStopBelie, 6)!;

        Assert.Contains(hits, h => h.Artist == "Teddy Swims");
    }

    [Fact]
    public void Shows_the_plainest_version_but_keeps_the_rank_of_the_first()
    {
        // The first Journey result is already plain; the first hit must still be Journey (Deezer's best match).
        var hits = DeezerSongSearchParser.Parse(DontStopBelie, 6)!;

        Assert.Equal("Journey", hits[0].Artist);
        Assert.Equal("The Essential Journey", hits[0].Album);
    }

    [Fact]
    public void Strips_live_session_asides_from_the_title()
    {
        var hits = DeezerSongSearchParser.Parse(ValerieAmy, 6)!;

        var amy = Assert.Single(hits, h => h.Artist == "Amy Winehouse");
        Assert.Equal("Valerie", amy.Title);
        Assert.Equal(6, amy.OtherVersions);
    }

    [Fact]
    public void Moves_a_featured_artist_from_the_title_onto_the_artist_line()
    {
        var hits = DeezerSongSearchParser.Parse(ValerieAmy, 6)!;

        Assert.Contains(hits, h => h is { Title: "Valerie", Artist: "Mark Ronson ft. Amy Winehouse" });
    }

    [Fact]
    public void Different_songs_sharing_a_word_stay_separate_in_ranked_order()
    {
        var hits = DeezerSongSearchParser.Parse(Brightside, 6)!;

        Assert.Equal(6, hits.Count);
        Assert.Equal(("Mr. Brightside", "The Killers", 1), (hits[0].Title, hits[0].Artist, hits[0].OtherVersions));
        Assert.Equal("Lil Peep", hits[1].Artist);
    }

    [Fact]
    public void Carries_album_and_cover_through()
    {
        var hit = DeezerSongSearchParser.Parse(Brightside, 1)!.Single();

        Assert.Equal("Direct Hits", hit.Album);
        Assert.Equal("https://cdn.test/0.jpg", hit.ThumbnailUrl);   // the fixture carries cover_medium only
        Assert.Equal("https://cdn.test/0.jpg", hit.ArtworkUrl);     // …so the kept cover falls back to it
    }

    [Fact]
    public void Keeps_the_big_cover_and_shows_the_medium_one()
    {
        const string json = """
            { "data": [ { "title": "Creep", "artist": { "name": "Radiohead" },
              "album": { "title": "Pablo Honey", "cover_small": "s", "cover_medium": "m", "cover_big": "b", "cover_xl": "xl" } } ] }
            """;

        var hit = DeezerSongSearchParser.Parse(json, 6)!.Single();

        Assert.Equal("b", hit.ArtworkUrl);
        Assert.Equal("m", hit.ThumbnailUrl);
    }

    [Fact]
    public void Honours_the_limit()
        => Assert.Equal(2, DeezerSongSearchParser.Parse(Brightside, 2)!.Count);

    [Theory]
    [InlineData("Mr. Brightside - Remastered 2011", "Mr. Brightside")]
    [InlineData("Mr. Brightside - Live", "Mr. Brightside")]
    [InlineData("Mr. Brightside [Live at Wembley]", "Mr. Brightside")]
    [InlineData("Ain't No Mountain - High Enough", "Ain't No Mountain - High Enough")]   // a dash that's part of the title stays
    public void Cleans_version_suffixes_but_not_real_dashes(string raw, string shown)
    {
        var json = $$"""{ "data": [ { "title": "{{raw}}", "artist": { "name": "X" }, "album": {} } ] }""";

        Assert.Equal(shown, DeezerSongSearchParser.Parse(json, 6)!.Single().Title);
    }

    [Fact]
    public void An_empty_data_array_is_no_results_not_a_failure()
        => Assert.Empty(DeezerSongSearchParser.Parse("""{ "data": [], "total": 0 }""", 6)!);

    [Theory]
    [InlineData("""{ "error": { "type": "Exception", "message": "Quota limit exceeded", "code": 4 } }""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("""{ "nope": 1 }""")]
    public void An_error_or_unusable_reply_is_a_failure(string json)
        => Assert.Null(DeezerSongSearchParser.Parse(json, 6));

    [Fact]
    public void Skips_rows_missing_a_title_or_artist()
    {
        const string json = """{ "data": [ { "title": "", "artist": { "name": "X" } }, { "title": "Song" }, { "title": "Ok", "artist": { "name": "Y" } } ] }""";

        Assert.Equal("Ok", Assert.Single(DeezerSongSearchParser.Parse(json, 6)!).Title);
    }
}
