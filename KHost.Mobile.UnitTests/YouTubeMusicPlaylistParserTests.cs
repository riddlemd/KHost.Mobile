using KHost.Mobile.Client.YouTubeMusic;
using Xunit;

namespace KHost.Mobile.UnitTests;

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
}
