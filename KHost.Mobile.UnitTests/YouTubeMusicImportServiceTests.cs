using System.Net;
using KHost.Mobile.Clients.YouTubeMusic;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The HTTP wrapper's error mapping + request shape; page parsing is covered by YouTubeMusicPlaylistParserTests.
public class YouTubeMusicImportServiceTests
{
    private const string PlaylistUrl = "https://music.youtube.com/playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM";

    private static YouTubeMusicImportService Service(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Rejects_a_link_that_is_not_a_yt_music_playlist_without_calling_the_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        await Assert.ThrowsAsync<YouTubeMusicImportException>(() => Service(handler).FetchPlaylistAsync("https://example.com/nope"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Requests_the_playlist_page_with_a_browser_user_agent_and_english_titles()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);   // short-circuit after the request

        await Assert.ThrowsAsync<YouTubeMusicImportException>(() => Service(handler).FetchPlaylistAsync(PlaylistUrl));

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("Mozilla", handler.LastRequest.Headers.UserAgent.ToString());
        Assert.Contains("en-US", handler.LastRequest.Headers.AcceptLanguage.ToString());
    }

    [Fact]
    public async Task Maps_a_server_error_to_a_domain_exception_with_the_status()
    {
        var ex = await Assert.ThrowsAsync<YouTubeMusicImportException>(
            () => Service(new StubHandler(HttpStatusCode.ServiceUnavailable)).FetchPlaylistAsync(PlaylistUrl));

        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task Wraps_a_transport_failure_and_a_request_timeout_as_domain_exceptions()
    {
        await Assert.ThrowsAsync<YouTubeMusicImportException>(
            () => Service(new ThrowingHandler(new HttpRequestException("down"))).FetchPlaylistAsync(PlaylistUrl));

        await Assert.ThrowsAsync<YouTubeMusicImportException>(
            () => Service(new ThrowingHandler(new TaskCanceledException())).FetchPlaylistAsync(PlaylistUrl));
    }

    [Fact]
    public async Task A_genuine_caller_cancellation_propagates_as_cancellation_not_a_network_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service(new StubHandler(HttpStatusCode.OK)).FetchPlaylistAsync(PlaylistUrl, cts.Token));
    }
}
