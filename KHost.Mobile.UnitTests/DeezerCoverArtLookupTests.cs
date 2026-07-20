using System.Net;
using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests;

// The HTTP wrapper's error mapping + request shape; result matching is covered by DeezerCoverArtParserTests.
public class DeezerCoverArtLookupTests
{
    private static DeezerCoverArtLookup Lookup(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Theory]
    [InlineData("   ", "Toto")]
    [InlineData("Africa", "  ")]
    public async Task A_blank_title_or_artist_returns_null_without_calling_the_network(string title, string artist)
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        Assert.Null(await Lookup(handler).FindCoverArtUrlAsync(title, artist));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Uses_a_field_scoped_query_so_deezer_narrows_to_the_song()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"data":[]}""");

        await Lookup(handler).FindCoverArtUrlAsync("Aeroplane", "Red Hot Chili Peppers");

        var url = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("artist:\"Red Hot Chili Peppers\"", url);
        Assert.Contains("track:\"Aeroplane\"", url);
    }

    [Fact]
    public async Task No_match_returns_null_rather_than_throwing()
    {
        Assert.Null(await Lookup(new StubHandler(HttpStatusCode.OK, """{"data":[]}""")).FindCoverArtUrlAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task Maps_rate_limiting_and_server_errors_to_domain_exceptions()
    {
        var rateLimited = await Assert.ThrowsAsync<DeezerCoverArtException>(
            () => Lookup(new StubHandler((HttpStatusCode)429)).FindCoverArtUrlAsync("Africa", "Toto"));
        Assert.Contains("rate-limited", rateLimited.Message);

        var serverError = await Assert.ThrowsAsync<DeezerCoverArtException>(
            () => Lookup(new StubHandler(HttpStatusCode.InternalServerError)).FindCoverArtUrlAsync("Africa", "Toto"));
        Assert.Contains("500", serverError.Message);
    }

    [Fact]
    public async Task Wraps_a_transport_failure_and_a_request_timeout_as_domain_exceptions()
    {
        await Assert.ThrowsAsync<DeezerCoverArtException>(
            () => Lookup(new ThrowingHandler(new HttpRequestException("down"))).FindCoverArtUrlAsync("Africa", "Toto"));

        await Assert.ThrowsAsync<DeezerCoverArtException>(
            () => Lookup(new ThrowingHandler(new TaskCanceledException())).FindCoverArtUrlAsync("Africa", "Toto"));
    }

    [Fact]
    public async Task A_genuine_caller_cancellation_propagates_as_cancellation_not_a_network_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Lookup(new StubHandler(HttpStatusCode.OK)).FindCoverArtUrlAsync("Africa", "Toto", cts.Token));
    }
}
