using KHost.Mobile.Clients.YouTubeMusic;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class YouTubeMusicPlaylistUrlTests
{
    private const string Id = "PL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i";

    [Theory]
    [InlineData("https://music.youtube.com/playlist?list=PL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i")]
    [InlineData("https://www.youtube.com/playlist?list=PL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i&si=abc")]
    [InlineData("https://music.youtube.com/browse/VLPL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i")]   // VL prefix stripped
    [InlineData("PL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i")]
    [InlineData("  PL4fGSI1pDJn6puJdseH2Rt9sMvt9E2M4i  ")]
    public void TryParseId_extracts_id_from_supported_shapes(string input)
    {
        Assert.True(YouTubeMusicPlaylistUrl.TryParseId(input, out var id));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("nope")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]   // a track, not a playlist
    [InlineData("dQw4w9WgXcQ")]                                      // an 11-char video id, too short for a playlist
    public void TryParseId_rejects_non_playlist_input(string? input)
    {
        Assert.False(YouTubeMusicPlaylistUrl.TryParseId(input, out var id));
        Assert.Equal(string.Empty, id);
    }
}
