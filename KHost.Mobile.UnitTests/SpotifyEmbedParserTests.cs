using System.Text;
using KHost.Mobile.Client.Spotify;
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
}
