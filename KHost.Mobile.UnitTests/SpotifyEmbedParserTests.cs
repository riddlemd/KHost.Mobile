using System.Text;
using KHost.Mobile.Clients.Spotify;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SpotifyEmbedParserTests
{
    // Minimal stand-in for the real embed page: the __NEXT_DATA__ blob with the entity/trackList
    // nested a few levels deep, so the recursive search — not a fixed path — is what's exercised.
    // Built by concatenation to avoid raw-string/JSON brace collisions.
    private static string HtmlWith(string trackListJson, string name = "Test Playlist")
        => "<html><body>\n<script id=\"__NEXT_DATA__\" type=\"application/json\">\n"
         + "{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":"
         + "{\"type\":\"playlist\",\"name\":\"" + name + "\",\"trackList\":" + trackListJson + "}"
         + "}}}}}\n"
         + "</script>\n</body></html>";

    [Fact]
    public void Parse_reads_name_title_artist_and_track_id()
    {
        var html = HtmlWith("""
            [
              {"uri":"spotify:track:1TfqLAPs4K3s2rJMoCokcS","title":"Sweet Dreams","subtitle":"Eurythmics"},
              {"uri":"spotify:track:0A4PZuepTcIQVvA5m7R0M1","title":"Don't You","subtitle":"Simple Minds"}
            ]
            """);

        var result = SpotifyEmbedParser.Parse(html);

        Assert.Equal("Test Playlist", result.Name);
        Assert.False(result.LikelyTruncated);
        Assert.Equal(2, result.Tracks.Count);

        Assert.Equal("Sweet Dreams", result.Tracks[0].Title);
        Assert.Equal("Eurythmics", result.Tracks[0].Artist);
        Assert.Equal("1TfqLAPs4K3s2rJMoCokcS", result.Tracks[0].SpotifyTrackId);
    }

    [Fact]
    public void Parse_skips_rows_without_a_title()
    {
        var html = HtmlWith("""
            [
              {"uri":"spotify:track:aaa","title":"Real Song","subtitle":"An Artist"},
              {"uri":"spotify:episode:bbb","title":"","subtitle":"A Podcast"}
            ]
            """);

        var result = SpotifyEmbedParser.Parse(html);

        Assert.Single(result.Tracks);
        Assert.Equal("Real Song", result.Tracks[0].Title);
    }

    [Fact]
    public void Parse_flags_truncation_at_100_tracks()
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < 100; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($$"""{"uri":"spotify:track:id{{i}}","title":"Song {{i}}","subtitle":"Artist {{i}}"}""");
        }
        sb.Append(']');

        var result = SpotifyEmbedParser.Parse(HtmlWith(sb.ToString()));

        Assert.Equal(100, result.Tracks.Count);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void Parse_throws_friendly_error_when_blob_is_missing()
    {
        var ex = Assert.Throws<SpotifyImportException>(() => SpotifyEmbedParser.Parse("<html><body>no data here</body></html>"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Parse_throws_when_there_is_no_tracklist()
    {
        var html = """
            <html><body>
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"state":{"data":{"entity":{"type":"playlist","name":"Empty"}}}}}}
            </script>
            </body></html>
            """;

        Assert.Throws<SpotifyImportException>(() => SpotifyEmbedParser.Parse(html));
    }

    [Fact]
    public void Parse_leaves_the_name_null_when_the_entity_is_not_a_playlist()
    {
        // TryFindPlaylistName only accepts a node whose "type" is literally "playlist" — an album or
        // show entity still carries a trackList, but its name must not be reported as the playlist name.
        var html = "<html><body>\n<script id=\"__NEXT_DATA__\" type=\"application/json\">\n"
            + "{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":"
            + "{\"type\":\"album\",\"name\":\"Some Album\",\"trackList\":"
            + """[{"uri":"spotify:track:aaa","title":"Song A","subtitle":"Artist A"}]"""
            + "}}}}}}\n</script>\n</body></html>";

        var result = SpotifyEmbedParser.Parse(html);

        Assert.Null(result.Name);
        Assert.Single(result.Tracks);
        Assert.Equal("Song A", result.Tracks[0].Title);
    }

    [Fact]
    public void Parse_includes_a_track_whose_uri_is_missing_or_not_a_track_uri()
    {
        var html = HtmlWith("""
            [
              {"title":"No Uri Song","subtitle":"Artist A"},
              {"uri":"spotify:episode:zzz","title":"Episode Uri Song","subtitle":"Artist B"}
            ]
            """);

        var result = SpotifyEmbedParser.Parse(html);

        Assert.Equal(2, result.Tracks.Count);
        Assert.Null(result.Tracks[0].SpotifyTrackId);
        Assert.Null(result.Tracks[1].SpotifyTrackId);
    }

    [Fact]
    public void Parse_descends_past_a_decoy_sibling_to_find_a_deeper_entity()
    {
        // The decoy sits beside the real entity and is visited first (document order); it must not
        // satisfy the name/array search itself, forcing the recursion one level deeper than HtmlWith's
        // fixed data.entity path to find the real playlist under data.wrapper.entity.
        var html = "<html><body>\n<script id=\"__NEXT_DATA__\" type=\"application/json\">\n"
            + "{\"props\":{\"pageProps\":{\"state\":{\"data\":{"
            + "\"decoy\":{\"type\":\"track\",\"name\":\"Not This\",\"somethingElse\":[1,2,3]},"
            + "\"wrapper\":{\"entity\":{\"type\":\"playlist\",\"name\":\"Deep Playlist\",\"trackList\":"
            + """[{"uri":"spotify:track:zzz","title":"Deep Song","subtitle":"Deep Artist"}]"""
            + "}}"
            + "}}}}}\n</script>\n</body></html>";

        var result = SpotifyEmbedParser.Parse(html);

        Assert.Equal("Deep Playlist", result.Name);
        Assert.Single(result.Tracks);
        Assert.Equal("Deep Song", result.Tracks[0].Title);
    }
}
