using System.Net;
using System.Text;
using KHost.Mobile.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

// The Debug body-logging seam: textual bodies are buffered (re-readable for the caller) and logged truncated;
// binary bodies are never touched; and a body with NO Content-Length (chunked/decompressed — the norm on the
// native Android stack) is capped by LoadIntoBufferAsync's explicit limit rather than buffering unbounded.
public sealed class LoggingHttpMessageHandlerTests
{
    private const string Url = "https://example.test/api";

    private static async Task<(HttpResponseMessage Response, CapturingLogger Log)> SendAsync(HttpContent content)
    {
        var log = new CapturingLogger();
        var handler = new LoggingHttpMessageHandler(log)
        {
            InnerHandler = new StubHandler(content),
        };
        using var invoker = new HttpMessageInvoker(handler);
        var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url), CancellationToken.None);
        return (response, log);
    }

    [Fact]
    public async Task Logs_a_textual_body_and_leaves_it_readable_for_the_caller()
    {
        var (response, log) = await SendAsync(new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json"));

        Assert.Contains(log.Messages, m => m.Contains("""{"ok":true}"""));
        // Buffered, not consumed: the caller can still read the same body after the handler logged it.
        Assert.Equal("""{"ok":true}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Never_reads_a_binary_body()
    {
        var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new("image/png");

        var (_, log) = await SendAsync(content);

        Assert.DoesNotContain(log.Messages, m => m.Contains("HTTP body"));
    }

    [Fact]
    public async Task An_oversize_body_without_a_content_length_is_not_logged_and_still_reaches_the_caller()
    {
        // > MaxBufferBytes (1 MB), served with NO Content-Length header — the shape the header-only guard missed.
        var big = new string('x', 1_200_000);
        var (response, log) = await SendAsync(new NoLengthContent(Encoding.UTF8.GetBytes(big), "application/json"));

        // The capped LoadIntoBufferAsync throws internally; the handler degrades to its "<unavailable>" note
        // instead of logging (or fully buffering) the megabyte body.
        Assert.Contains(log.Messages, m => m.Contains("<unavailable>"));
        Assert.DoesNotContain(log.Messages, m => m.Contains("xxxxxxxxxx"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // the response itself is returned regardless
    }

    // ---- test doubles --------------------------------------------------------------------------

    private sealed class StubHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    // Streams bytes without ever declaring a Content-Length, like a chunked/decompressed response.
    private sealed class NoLengthContent : HttpContent
    {
        private readonly byte[] _data;

        public NoLengthContent(byte[] data, string mediaType)
        {
            _data = data;
            Headers.ContentType = new(mediaType);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => await stream.WriteAsync(_data);

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    // Minimal ILogger capturing formatted messages; Debug enabled so the body path actually runs.
    private sealed class CapturingLogger : ILogger<LoggingHttpMessageHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
