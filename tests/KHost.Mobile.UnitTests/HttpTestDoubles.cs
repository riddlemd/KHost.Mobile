using System.Net;
using System.Text;

namespace KHost.Mobile.UnitTests;

// Shared HTTP test doubles for the typed-client tests (the request-building / status-mapping / cancellation
// layer over the already-tested pure parsers). Mirrors the stub pattern in the integration tests'
// AlbumArtCacheTests: no network, no I/O — a canned response or a thrown transport exception.
internal sealed class StubHandler(HttpStatusCode status, string body = "", string mediaType = "application/json") : HttpMessageHandler
{
    /// <summary>The last request seen, so tests can assert the URL/headers the client actually built.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Honor the token like a real transport would — a synchronously-completing stub otherwise races past an
        // already-cancelled token, and the cancellation-contract tests would never see the cancellation surface.
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }
}

// Simulates a transport failure (HttpRequestException) or a request timeout (TaskCanceledException with an
// UNCANCELLED caller token) — the two shapes the clients' catch filters must map to domain exceptions.
internal sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(exception);
}
