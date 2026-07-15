using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.Diagnostics;

/// <summary>
/// A <see cref="DelegatingHandler"/> that logs every outbound request and its response for the app's typed
/// <c>HttpClient</c>s — method, URL, status, elapsed time, and (at <see cref="LogLevel.Debug"/>) a truncated
/// response body. Attached to the HTTP clients in <c>MauiProgram</c>.
/// </summary>
/// <remarks>
/// This is the single seam that captures what the <em>native platform</em> HTTP stack actually sends and receives
/// on-device (Android <c>HttpClientHandler</c> → OkHttp), which a desktop reproduction can miss. When an external
/// lookup "works on my machine" but returns nothing on the phone, the Debug-level body log is what tells the two
/// apart. Body logging only touches textual responses (never an image download) and never lets a logging failure
/// break the request.
/// </remarks>
public sealed class LoggingHttpMessageHandler(ILogger<LoggingHttpMessageHandler> logger) : DelegatingHandler
{
    // Cap the logged body so a large payload can't flood logcat — enough to see an API's JSON shape or error object.
    private const int MaxBodyChars = 4000;

    // Don't buffer a response we'd never fully log anyway (defensive: our textual APIs return a few KB).
    private const long MaxBufferBytes = 1024 * 1024;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        logger.LogInformation("HTTP → {Method} {Uri}", request.Method, uri);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Includes the native transport failures the callers turn into domain exceptions (e.g. the
            // "connection failed" that a foreign network path can surface). Logged before it propagates.
            logger.LogWarning(ex, "HTTP ✗ {Method} {Uri} threw after {Elapsed} ms", request.Method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();
        logger.LogInformation("HTTP ← {Status} {Method} {Uri} in {Elapsed} ms",
            (int)response.StatusCode, request.Method, uri, stopwatch.ElapsedMilliseconds);

        await LogBodyIfTextualAsync(request, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    // Debug-only, best-effort: buffer the (textual) content so reading a snippet here doesn't consume it for the
    // caller, then log a truncated copy. Any failure degrades to a single note — the response is returned regardless.
    private async Task LogBodyIfTextualAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!logger.IsEnabled(LogLevel.Debug) || !IsTextual(response.Content.Headers.ContentType))
            return;
        if (response.Content.Headers.ContentLength is > MaxBufferBytes)
            return;

        try
        {
            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);   // re-readable for the caller
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("HTTP body {Method} {Uri}: {Body}", request.Method, request.RequestUri, Truncate(body));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "HTTP body {Method} {Uri}: <unavailable>", request.Method, request.RequestUri);
        }
    }

    // Only buffer/log text bodies — never an image download (the album-art client) or other binary payload.
    private static bool IsTextual(MediaTypeHeaderValue? contentType)
    {
        var mediaType = contentType?.MediaType;
        if (string.IsNullOrEmpty(mediaType))
            return false;

        return mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string body)
        => body.Length <= MaxBodyChars ? body : $"{body[..MaxBodyChars]}… (+{body.Length - MaxBodyChars} more chars)";
}
