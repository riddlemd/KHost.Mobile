using System.Net;
using KHost.Mobile.Clients.Updates;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The update check is best-effort by design: every failure shape must come back as "nothing new" (null), never
// an exception — EXCEPT a genuine caller cancellation. Feed parsing is covered by GitHubReleaseParserTests.
public class GitHubReleaseClientTests
{
    private static GitHubReleaseClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });

    [Fact]
    public async Task A_transport_failure_is_swallowed_as_nothing_new()
    {
        Assert.Null(await Client(new ThrowingHandler(new HttpRequestException("down"))).GetNewestReleaseAsync());
    }

    [Fact]
    public async Task A_non_success_status_is_swallowed_as_nothing_new()
    {
        Assert.Null(await Client(new StubHandler(HttpStatusCode.Forbidden)).GetNewestReleaseAsync());
    }

    [Fact]
    public async Task An_empty_release_feed_is_nothing_new()
    {
        Assert.Null(await Client(new StubHandler(HttpStatusCode.OK, "[]")).GetNewestReleaseAsync());
    }

    [Fact]
    public async Task A_genuine_caller_cancellation_propagates_rather_than_reading_as_nothing_new()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Client(new StubHandler(HttpStatusCode.OK)).GetNewestReleaseAsync(cts.Token));
    }
}
