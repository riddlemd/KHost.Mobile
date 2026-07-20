using System.Net;
using KHost.Mobile.Clients.Spotify;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The HTTP wrapper's error mapping + request shape; the embed parsing itself is covered by SpotifyEmbedParserTests.
public class SpotifyImportServiceTests
{
    private const string PlaylistUrl = "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M";

    private static SpotifyImportService Service(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Rejects_a_link_that_is_not_a_spotify_playlist_without_calling_the_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        await Assert.ThrowsAsync<SpotifyImportException>(() => Service(handler).FetchPlaylistAsync("https://example.com/nope"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Requests_the_embed_page_with_a_browser_user_agent()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound);   // short-circuit after the request we inspect

        await Assert.ThrowsAsync<SpotifyImportException>(() => Service(handler).FetchPlaylistAsync(PlaylistUrl));

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("/embed/playlist/37i9dQZF1DXcBWIGoYBM5M", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("Mozilla", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Maps_404_to_a_not_found_message()
    {
        var ex = await Assert.ThrowsAsync<SpotifyImportException>(
            () => Service(new StubHandler(HttpStatusCode.NotFound)).FetchPlaylistAsync(PlaylistUrl));

        Assert.Contains("couldn't be found", ex.Message);
    }

    [Fact]
    public async Task Maps_a_server_error_to_a_domain_exception_with_the_status()
    {
        var ex = await Assert.ThrowsAsync<SpotifyImportException>(
            () => Service(new StubHandler(HttpStatusCode.InternalServerError)).FetchPlaylistAsync(PlaylistUrl));

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Wraps_a_transport_failure_and_a_request_timeout_as_domain_exceptions()
    {
        await Assert.ThrowsAsync<SpotifyImportException>(
            () => Service(new ThrowingHandler(new HttpRequestException("down"))).FetchPlaylistAsync(PlaylistUrl));

        // A timeout surfaces as TaskCanceledException with an UNCANCELLED caller token — still a domain error.
        await Assert.ThrowsAsync<SpotifyImportException>(
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
