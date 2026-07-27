using System.Net;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Deezer;

public class DeezerSpellingSuggestionLookupTests
{
    private const string OneHit = """{ "data": [ { "title": "Creep", "artist": { "name": "Radiohead" } } ] }""";

    private static DeezerSpellingSuggestionLookup Lookup(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Searches_with_the_plain_query_not_the_field_scoped_one()
    {
        // The whole reason this is a separate call: measured against the live API, the field-scoped
        // `artist:"…" track:"…"` form the cover-art lookup uses returns ZERO results for a typo, while the
        // plain free-text form corrects it. Sending the scoped form here would silently kill the feature.
        var handler = new StubHandler(HttpStatusCode.OK, OneHit);

        await Lookup(handler).SuggestAsync("Creap", "Radiohead");

        var url = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.DoesNotContain("artist%3A", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("track%3A", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("q=Radiohead%20Creap", url);
    }

    [Fact]
    public async Task Returns_the_parsed_suggestion_on_a_hit()
    {
        var suggestion = await Lookup(new StubHandler(HttpStatusCode.OK, OneHit)).SuggestAsync("Creap", "Radiohead");

        Assert.Equal("Creep", suggestion!.Title);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData((HttpStatusCode)429)]
    public async Task Degrades_to_no_suggestion_on_an_http_failure(HttpStatusCode status)
    {
        // A hint is best-effort: it must never surface an error to the auto-fill path.
        Assert.Null(await Lookup(new StubHandler(status)).SuggestAsync("Creap", "Radiohead"));
    }

    [Fact]
    public async Task Degrades_to_no_suggestion_on_a_transport_failure()
        => Assert.Null(await Lookup(new ThrowingHandler(new HttpRequestException("offline")))
            .SuggestAsync("Creap", "Radiohead"));

    [Fact]
    public async Task Degrades_to_no_suggestion_on_a_request_timeout()
        => Assert.Null(await Lookup(new ThrowingHandler(new TaskCanceledException()))
            .SuggestAsync("Creap", "Radiohead"));

    [Fact]
    public async Task A_caller_cancellation_still_surfaces()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Lookup(new StubHandler(HttpStatusCode.OK, OneHit)).SuggestAsync("Creap", "Radiohead", cts.Token));
    }

    [Theory]
    [InlineData("", "Radiohead")]
    [InlineData("Creap", "")]
    public async Task A_blank_side_returns_nothing_without_calling_the_network(string title, string artist)
    {
        var handler = new StubHandler(HttpStatusCode.OK, OneHit);

        Assert.Null(await Lookup(handler).SuggestAsync(title, artist));

        Assert.Null(handler.LastRequest);
    }
}
