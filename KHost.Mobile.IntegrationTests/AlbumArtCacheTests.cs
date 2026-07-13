using System.Net;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

/// <summary>
/// Exercises <see cref="AlbumArtCache"/> against a real temp folder with a stubbed HTTP layer, so the
/// download → disk-cache → base64 data-URI path (and clear/count) is verified without touching the network.
/// </summary>
public sealed class AlbumArtCacheTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private AlbumArtCache NewCache(StubHandler handler) => new(_dir, new StubHttpClientFactory(handler));

    [Fact]
    public async Task Downloads_caches_and_returns_a_base64_data_uri()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new StubHandler(bytes);
        var cache = NewCache(handler);

        var uri = await cache.GetDataUriAsync("https://example.com/cover/300x300bb.jpg");

        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}", uri);
        Assert.Equal(1, await cache.CountAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_repeat_request_is_served_from_memory_without_a_second_download()
    {
        var handler = new StubHandler([9, 9]);
        var cache = NewCache(handler);

        var first = await cache.GetDataUriAsync("https://example.com/a.jpg");
        var second = await cache.GetDataUriAsync("https://example.com/a.jpg");

        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);   // second call hit the in-memory memo
    }

    [Fact]
    public async Task A_second_instance_reads_the_cached_image_from_disk_without_downloading()
    {
        const string url = "https://example.com/persist.jpg";
        var bytes = new byte[] { 7, 7, 7 };
        await NewCache(new StubHandler(bytes)).GetDataUriAsync(url);

        var handler2 = new StubHandler([]);   // a fresh instance with an empty payload, to prove it isn't hit
        var uri = await NewCache(handler2).GetDataUriAsync(url);

        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}", uri);
        Assert.Equal(0, handler2.Calls);      // served from the on-disk cache
    }

    [Fact]
    public async Task ClearAsync_empties_the_cache_and_raises_Changed()
    {
        var cache = NewCache(new StubHandler([5]));
        await cache.GetDataUriAsync("https://example.com/one.jpg");
        Assert.Equal(1, await cache.CountAsync());

        var changed = 0;
        cache.Changed += (_, _) => changed++;
        await cache.ClearAsync();

        Assert.Equal(0, await cache.CountAsync());
        Assert.True(changed >= 1);
    }

    [Fact]
    public async Task A_blank_url_returns_null_and_never_downloads()
    {
        var handler = new StubHandler([1]);
        var cache = NewCache(handler);

        Assert.Null(await cache.GetDataUriAsync(null));
        Assert.Null(await cache.GetDataUriAsync("   "));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_failed_download_returns_null_and_caches_nothing()
    {
        var handler = new StubHandler([], HttpStatusCode.NotFound);
        var cache = NewCache(handler);

        Assert.Null(await cache.GetDataUriAsync("https://example.com/missing.jpg"));
        Assert.Equal(0, await cache.CountAsync());
    }

    // ---- test doubles --------------------------------------------------------------------------

    private sealed class StubHandler(byte[] payload, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(payload) });
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
