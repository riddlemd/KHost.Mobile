using System.Net;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

/// <summary>
/// Exercises <see cref="AlbumArtCache"/> against a real temp folder with a stubbed HTTP layer, so the
/// download → disk-cache → readable-stream path (and clear/count) is verified without touching the network.
/// </summary>
public sealed class AlbumArtCacheTests : IDisposable
{
    private readonly TempAppDataDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private AlbumArtCache NewCache(StubHandler handler) => new(_dir, new StubHttpClientFactory(handler));

    // Read (and dispose) a cover stream to its bytes; null passes through so a "no art" result stays null.
    private static async Task<byte[]?> ReadAllAsync(Stream? stream)
    {
        if (stream is null)
            return null;
        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
    }

    [Fact]
    public async Task Downloads_caches_and_returns_the_image_bytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new StubHandler(bytes);
        var cache = NewCache(handler);

        var got = await ReadAllAsync(await cache.OpenArtStreamAsync("https://example.com/cover/300x300bb.jpg"));

        Assert.Equal(bytes, got);
        Assert.Equal(1, await cache.CountAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_repeat_request_re_reads_from_disk_without_a_second_download()
    {
        var handler = new StubHandler([9, 9]);
        var cache = NewCache(handler);

        var first = await ReadAllAsync(await cache.OpenArtStreamAsync("https://example.com/a.jpg"));
        var second = await ReadAllAsync(await cache.OpenArtStreamAsync("https://example.com/a.jpg"));

        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);   // the second request re-opened the on-disk file, no re-download
    }

    [Fact]
    public async Task A_second_instance_reads_the_cached_image_from_disk_without_downloading()
    {
        const string url = "https://example.com/persist.jpg";
        var bytes = new byte[] { 7, 7, 7 };
        (await NewCache(new StubHandler(bytes)).OpenArtStreamAsync(url))?.Dispose();

        var handler2 = new StubHandler([]);   // a fresh instance with an empty payload, to prove it isn't hit
        var got = await ReadAllAsync(await NewCache(handler2).OpenArtStreamAsync(url));

        Assert.Equal(bytes, got);
        Assert.Equal(0, handler2.Calls);      // served from the on-disk cache
    }

    [Fact]
    public async Task ClearAsync_empties_the_cache_and_raises_Changed()
    {
        var cache = NewCache(new StubHandler([5]));
        (await cache.OpenArtStreamAsync("https://example.com/one.jpg"))?.Dispose();
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

        Assert.Null(await cache.OpenArtStreamAsync(null));
        Assert.Null(await cache.OpenArtStreamAsync("   "));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_failed_download_returns_null_and_caches_nothing()
    {
        var handler = new StubHandler([], HttpStatusCode.NotFound);
        var cache = NewCache(handler);

        Assert.Null(await cache.OpenArtStreamAsync("https://example.com/missing.jpg"));
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
