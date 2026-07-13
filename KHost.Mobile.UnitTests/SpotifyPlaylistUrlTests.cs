using KHost.Mobile.Client.Spotify;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SpotifyPlaylistUrlTests
{
    private const string Id = "72oItsIhq3OSIz88818IHj";   // real 22-char base62 id shape

    [Theory]
    [InlineData("https://open.spotify.com/playlist/72oItsIhq3OSIz88818IHj")]
    [InlineData("https://open.spotify.com/playlist/72oItsIhq3OSIz88818IHj?si=abc123&pi=x")]
    [InlineData("http://open.spotify.com/playlist/72oItsIhq3OSIz88818IHj")]
    [InlineData("spotify:playlist:72oItsIhq3OSIz88818IHj")]
    [InlineData("72oItsIhq3OSIz88818IHj")]
    [InlineData("  72oItsIhq3OSIz88818IHj  ")]
    public void TryParseId_extracts_id_from_supported_shapes(string input)
    {
        Assert.True(SpotifyPlaylistUrl.TryParseId(input, out var id));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("hello world")]
    [InlineData("https://open.spotify.com/track/1TfqLAPs4K3s2rJMoCokcS")]   // a track, not a playlist
    [InlineData("https://open.spotify.com/album/1TfqLAPs4K3s2rJMoCokcS")]   // an album, not a playlist
    [InlineData("spotify:playlist:tooshort")]
    public void TryParseId_rejects_non_playlist_input(string? input)
    {
        Assert.False(SpotifyPlaylistUrl.TryParseId(input, out var id));
        Assert.Equal(string.Empty, id);
    }
}
