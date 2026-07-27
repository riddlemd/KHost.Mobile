using System.Net;
using KHost.Mobile.Clients.Apple;
using KHost.Mobile.Clients.Metadata;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Apple;

// The HTTP wrapper's error mapping + request shape; result matching is covered by ITunesResponseParserTests.
public class ITunesTrackMetadataLookupTests
{
    private static ITunesTrackMetadataLookup Lookup(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task A_blank_title_returns_nothing_without_calling_the_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        Assert.Null((await Lookup(handler).LookupAsync("   ", "Toto")).Match);

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Searches_for_artist_and_title_together()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        await Lookup(handler).LookupAsync("Africa", "Toto");

        Assert.Contains("term=toto%20africa", handler.LastRequest!.RequestUri!.AbsoluteUri);   // ToString() would unescape
    }

    [Fact]
    public async Task Strips_punctuation_out_of_the_search_term()
    {
        // Measured against the live API: "All Time Low Dear Maria, Count Me Inn" returns nothing, while the
        // same query without the comma finds the song. The term is only for retrieval — every candidate is
        // re-verified against the raw title/artist — so searching the normalized text costs nothing.
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        await Lookup(handler).LookupAsync("Dear Maria, Count Me Inn", "All Time Low");

        var url = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.DoesNotContain("%2C", url);   // the comma, percent-encoded
        Assert.Contains("term=all%20time%20low%20dear%20maria%20count%20me%20inn", url);
    }

    [Fact]
    public async Task Falls_back_to_the_raw_text_when_normalizing_empties_it()
    {
        // "(Nice Dream)" is entirely a bracketed aside, so the normalizer returns nothing to search on.
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        await Lookup(handler).LookupAsync("(Nice Dream)", "Radiohead");

        Assert.Contains("Nice%20Dream", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task No_match_returns_nothing_rather_than_throwing()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"resultCount":0,"results":[]}""");

        var result = await Lookup(handler).LookupAsync("Africa", "Toto");

        Assert.Null(result.Match);
        Assert.Null(result.Suggestion);
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
