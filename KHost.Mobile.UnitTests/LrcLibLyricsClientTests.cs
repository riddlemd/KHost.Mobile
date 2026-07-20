using System.Net;
using KHost.Mobile.Clients.Lyrics;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The HTTP wrapper's error mapping; result parsing is covered by LrcLibResponseParserTests. The client uses a
// relative search path, so the stub client carries the base address DI would normally configure.
public class LrcLibLyricsClientTests
{
    private static LrcLibLyricsClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.net/") });

    [Fact]
    public async Task A_blank_title_returns_null_without_calling_the_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        Assert.Null(await Client(handler).FetchAsync("  ", "Toto"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Not_found_returns_null_rather_than_throwing()
    {
        // 404 is the COMMON outcome of a lyrics lookup — "no lyrics" is a result, not an error.
        Assert.Null(await Client(new StubHandler(HttpStatusCode.NotFound)).FetchAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task An_empty_result_set_returns_null()
    {
        Assert.Null(await Client(new StubHandler(HttpStatusCode.OK, "[]")).FetchAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task Maps_rate_limiting_and_server_errors_to_domain_exceptions()
    {
        var rateLimited = await Assert.ThrowsAsync<LyricsLookupException>(
            () => Client(new StubHandler((HttpStatusCode)429)).FetchAsync("Africa", "Toto"));
        Assert.Contains("rate-limited", rateLimited.Message);

        var serverError = await Assert.ThrowsAsync<LyricsLookupException>(
            () => Client(new StubHandler(HttpStatusCode.InternalServerError)).FetchAsync("Africa", "Toto"));
        Assert.Contains("500", serverError.Message);
    }

    [Fact]
    public async Task Wraps_a_transport_failure_and_a_request_timeout_as_domain_exceptions()
    {
        await Assert.ThrowsAsync<LyricsLookupException>(
            () => Client(new ThrowingHandler(new HttpRequestException("down"))).FetchAsync("Africa", "Toto"));

        await Assert.ThrowsAsync<LyricsLookupException>(
            () => Client(new ThrowingHandler(new TaskCanceledException())).FetchAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task A_genuine_caller_cancellation_propagates_as_cancellation_not_a_network_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Client(new StubHandler(HttpStatusCode.OK)).FetchAsync("Africa", "Toto", cts.Token));
    }
}
