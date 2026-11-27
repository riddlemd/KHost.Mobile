using System.Net;
using KHost.Mobile.Clients.Deezer;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.Deezer;

public class DeezerSongSearchTests
{
    private const string OneHit = """{ "data": [ { "title": "Creep", "artist": { "name": "Radiohead" }, "album": { "title": "Pablo Honey" } } ] }""";

    private static DeezerSongSearch Search(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Sends_artist_then_title_as_one_plain_free_text_query()
    {
        // Free text, not the field-scoped artist:"" track:"" form: that one is exact-only and returns nothing for
        // a typo or a half-typed title, which is the whole point of searching.
        var handler = new StubHandler(HttpStatusCode.OK, OneHit);

        await Search(handler).SearchAsync("creap", "radiohead");

        var url = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("q=radiohead%20creap", url);
        Assert.DoesNotContain("track%3A", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Searches_on_the_title_alone_when_no_artist_is_given()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OneHit);

        await Search(handler).SearchAsync("  creep ", null);

        Assert.Contains("q=creep&", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Returns_the_parsed_hits()
    {
        var hits = await Search(new StubHandler(HttpStatusCode.OK, OneHit)).SearchAsync("creep", null);

        Assert.Equal("Creep", Assert.Single(hits!).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task A_too_short_title_is_an_empty_result_without_a_request(string title)
    {
        var handler = new StubHandler(HttpStatusCode.OK, OneHit);

        Assert.Empty((await Search(handler).SearchAsync(title, "Radiohead"))!);
        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData((HttpStatusCode)429)]
    public async Task An_http_failure_is_null_so_the_panel_can_say_search_is_unavailable(HttpStatusCode status)
        => Assert.Null(await Search(new StubHandler(status)).SearchAsync("creep", null));

    [Fact]
    public async Task Being_offline_is_null()
        => Assert.Null(await Search(new ThrowingHandler(new HttpRequestException("offline"))).SearchAsync("creep", null));

    [Fact]
    public async Task A_timeout_is_null()
        => Assert.Null(await Search(new ThrowingHandler(new TaskCanceledException())).SearchAsync("creep", null));

    [Fact]
    public async Task A_newer_keystroke_cancelling_the_search_still_surfaces()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Search(new ThrowingHandler(new TaskCanceledException())).SearchAsync("creep", null, cancellationToken: cts.Token));
    }
}
