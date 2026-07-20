using System.Net;
using KHost.Mobile.Clients.Enrichment;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The HTTP wrapper's error mapping + request shape; result matching is covered by ITunesResponseParserTests.
public class ITunesTrackMetadataLookupTests
{
    private static ITunesTrackMetadataLookup Lookup(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task A_blank_title_returns_null_without_calling_the_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        Assert.Null(await Lookup(handler).LookupAsync("   ", "Toto"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Searches_for_artist_and_title_together()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        await Lookup(handler).LookupAsync("Africa", "Toto");

        Assert.Contains("term=Toto%20Africa", handler.LastRequest!.RequestUri!.AbsoluteUri);   // ToString() would unescape
    }

    [Fact]
    public async Task No_match_returns_null_rather_than_throwing()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        Assert.Null(await Lookup(handler).LookupAsync("Africa", "Toto"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData((HttpStatusCode)429)]
    public async Task Maps_rate_limiting_to_the_rate_limit_message(HttpStatusCode status)
    {
        var ex = await Assert.ThrowsAsync<MetadataLookupException>(
            () => Lookup(new StubHandler(status)).LookupAsync("Africa", "Toto"));

        Assert.Contains("rate-limited", ex.Message);
    }

    [Fact]
    public async Task Maps_a_server_error_to_a_domain_exception_with_the_status()
    {
        var ex = await Assert.ThrowsAsync<MetadataLookupException>(
            () => Lookup(new StubHandler(HttpStatusCode.InternalServerError)).LookupAsync("Africa", "Toto"));

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Wraps_a_transport_failure_and_a_request_timeout_as_domain_exceptions()
    {
        await Assert.ThrowsAsync<MetadataLookupException>(
            () => Lookup(new ThrowingHandler(new HttpRequestException("down"))).LookupAsync("Africa", "Toto"));

        await Assert.ThrowsAsync<MetadataLookupException>(
            () => Lookup(new ThrowingHandler(new TaskCanceledException())).LookupAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task A_genuine_caller_cancellation_propagates_as_cancellation_not_a_network_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Lookup(new StubHandler(HttpStatusCode.OK)).LookupAsync("Africa", "Toto", cts.Token));
    }
}
