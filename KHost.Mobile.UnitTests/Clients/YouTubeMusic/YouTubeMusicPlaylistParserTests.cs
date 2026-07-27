using KHost.Mobile.Clients.YouTubeMusic;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.YouTubeMusic;

public class YouTubeMusicPlaylistParserTests
{
    // Two catalog rows: column 0 = title, column 1 = artist (multiple runs to test concatenation),
    // plus playlistItemData.videoId. No apostrophes/backslashes so it embeds in a single-quoted JS blob.
    private const string TwoTracksJson = """
    {
      "contents": [
        {
          "musicResponsiveListItemRenderer": {
            "flexColumns": [
              { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Waka Waka" } ] } } },
              { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Shakira" }, { "text": " & " }, { "text": "Burna Boy" } ] } } }
            ],
            "playlistItemData": { "videoId": "pRpeEdMmmQ0" }
          }
        },
        {
          "musicResponsiveListItemRenderer": {
            "flexColumns": [
              { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Life Goes On" } ] } } },
              { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Oliver Tree" } ] } } }
            ],
            "playlistItemData": { "videoId": "8F2s8ivKXNY" }
          }
        }
      ]
    }
    """;

    // Wraps a JSON string in the same envelope YT Music uses: initialData.push({ data: '<js-string>' }).
    private static string Page(string dataJson, string name = "My Test Playlist")
        => "<html><head><title>" + name + " - YouTube Music</title></head><body>"
         + "<script>initialData.push({data: '" + dataJson + "'});</script></body></html>";

    // Hex-escape the double quotes the way YT Music does, to exercise the \xNN un-escaper.
    private static string HexEscapeQuotes(string json) => json.Replace("\"", "\\x22");

    [Fact]
    public void Parse_reads_name_title_artist_and_video_id()
    {
        var result = YouTubeMusicPlaylistParser.Parse(Page(TwoTracksJson));

        Assert.Equal("My Test Playlist", result.Name);
        Assert.False(result.LikelyTruncated);
        Assert.Equal(2, result.Tracks.Count);

        Assert.Equal("Waka Waka", result.Tracks[0].Title);
        Assert.Equal("Shakira & Burna Boy", result.Tracks[0].Artist);   // runs concatenated
        Assert.Equal("pRpeEdMmmQ0", result.Tracks[0].VideoId);
    }

    [Fact]
    public void Parse_decodes_hex_escaped_data_blob()
    {
        var result = YouTubeMusicPlaylistParser.Parse(Page(HexEscapeQuotes(TwoTracksJson)));

        Assert.Equal(2, result.Tracks.Count);
        Assert.Equal("Shakira & Burna Boy", result.Tracks[0].Artist);
    }

    [Fact]
    public void Parse_skips_rows_missing_a_title_or_video_id()
    {
        const string json = """
        {
          "contents": [
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Good Song" } ] } } } ],
                "playlistItemData": { "videoId": "pRpeEdMmmQ0" } } },
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "No Video Id" } ] } } } ] } },
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [] } } } ],
                "playlistItemData": { "videoId": "8F2s8ivKXNY" } } }
          ]
        }
        """;

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Single(result.Tracks);
        Assert.Equal("Good Song", result.Tracks[0].Title);
    }

    [Fact]
    public void Parse_flags_truncation_when_a_continuation_is_present()
    {
        const string json = """
        {
          "contents": [
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Only Song" } ] } } } ],
                "playlistItemData": { "videoId": "pRpeEdMmmQ0" } } },
            { "continuationItemRenderer": { "trigger": "CONTINUATION_TRIGGER_ON_ITEM_SHOWN" } }
          ]
        }
        """;

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Single(result.Tracks);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void Parse_throws_when_no_data_blob_is_present()
    {
        Assert.Throws<YouTubeMusicImportException>(
            () => YouTubeMusicPlaylistParser.Parse("<html><body>nothing to see</body></html>"));
    }

    [Fact]
    public void Parse_throws_when_the_blob_has_no_tracks()
    {
        Assert.Throws<YouTubeMusicImportException>(
            () => YouTubeMusicPlaylistParser.Parse(Page("""{ "contents": [] }""")));
    }

    [Fact]
    public void Parse_falls_back_to_a_nested_watch_endpoint_when_playlistItemData_has_no_video_id()
    {
        const string json = """
        {
          "contents": [
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Fallback Song" } ] } } } ],
                "someNested": { "watchEndpoint": { "videoId": "AbCdEfGhIjK" } }
            } }
          ]
        }
        """;

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Single(result.Tracks);
        Assert.Equal("Fallback Song", result.Tracks[0].Title);
        Assert.Equal("AbCdEfGhIjK", result.Tracks[0].VideoId);
    }

    [Fact]
    public void Parse_drops_a_row_whose_video_id_is_malformed_and_has_no_fallback()
    {
        // Actual behavior, not the guessed one: FromRow returns null for the whole row when VideoId is
        // null, so a malformed id drops the TRACK entirely — it isn't kept with VideoId == null.
        const string json = """
        {
          "contents": [
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Good Song" } ] } } } ],
                "playlistItemData": { "videoId": "pRpeEdMmmQ0" } } },
            { "musicResponsiveListItemRenderer": {
                "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Bad Id Song" } ] } } } ],
                "playlistItemData": { "videoId": "short" } } }
          ]
        }
        """;

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Single(result.Tracks);
        Assert.Equal("Good Song", result.Tracks[0].Title);
    }

    [Fact]
    public void Parse_picks_whichever_blob_parses_to_more_tracks()
    {
        const string oneTrackJson = """
        { "contents": [ { "musicResponsiveListItemRenderer": { "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "Solo Song" } ] } } } ], "playlistItemData": { "videoId": "abcdefghijk" } } } ] }
        """;

        var html = "<html><head><title>Two Blobs Test - YouTube Music</title></head><body>"
            + "<script>initialData.push({data: '" + oneTrackJson + "'});</script>"
            + "<script>initialData.push({data: '" + TwoTracksJson + "'});</script>"
            + "</body></html>";

        var result = YouTubeMusicPlaylistParser.Parse(html);

        Assert.Equal(2, result.Tracks.Count);
        Assert.Equal("Waka Waka", result.Tracks[0].Title);
    }

    [Fact]
    public void Parse_strips_a_bare_YouTube_suffix_and_decodes_html_entities_in_the_title()
    {
        var html = "<html><head><title>My Playlist &amp; Friends - YouTube</title></head><body>"
            + "<script>initialData.push({data: '" + TwoTracksJson + "'});</script></body></html>";

        var result = YouTubeMusicPlaylistParser.Parse(html);

        Assert.Equal("My Playlist & Friends", result.Name);
    }

    [Fact]
    public void JsUnescape_drops_the_backslash_before_an_unrecognized_escape_like_ampersand()
    {
        // "\&" isn't one of the recognized escapes (x/u/n/t/r/b/f), so the default case keeps just the
        // character after the backslash — proven here by an escaped ampersand surviving into the title.
        const string json = """
        { "contents": [ { "musicResponsiveListItemRenderer": { "flexColumns": [ { "musicResponsiveListItemFlexColumnRenderer": { "text": { "runs": [ { "text": "AT\&T" } ] } } } ], "playlistItemData": { "videoId": "abcdefghijk" } } } ] }
        """;

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Single(result.Tracks);
        Assert.Equal("AT&T", result.Tracks[0].Title);
    }

    [Fact]
    public void JsUnescape_turns_a_backslash_n_into_an_actual_newline()
    {
        // Insert a literal `\n` (backslash + n) as whitespace right after the opening brace. If JsUnescape
        // did not convert it to a real newline, the raw "\n" would be invalid JSON and this blob would be
        // dropped — parsing succeeding at all is the proof.
        var json = TwoTracksJson.Insert(1, "\\n");

        var result = YouTubeMusicPlaylistParser.Parse(Page(json));

        Assert.Equal(2, result.Tracks.Count);
    }
}
