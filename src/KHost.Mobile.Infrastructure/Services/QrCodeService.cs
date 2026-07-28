using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Net.Codecrete.QrCodeGenerator;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by Net.Codecrete.QrCodeGenerator (MIT, no dependencies). Error correction is
/// <see cref="QrCode.Ecc.Medium"/>: the higher levels buy resistance to physical damage (dirt, print smudging)
/// that a code on a screen can't suffer, and pay for it with denser modules — which is what actually costs a
/// scan at arm's length.
/// </remarks>
// logger is optional so a test can `new` the service without a logging stack; DI supplies the real one.
internal sealed class QrCodeService(ILogger<QrCodeService>? logger = null) : IQrCodeService
{
    // The spec's quiet zone. A caller's own padding adds more, but a scanner measures it in modules.
    private const int QuietZoneModules = 4;

    private readonly ILogger _log = logger ?? NullLogger<QrCodeService>.Instance;

    /// <inheritdoc />
    public string ToSvg(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _log.LogDebug("Encoding QR code for {Text}", text);
        var svg = QrCode.EncodeText(text, QrCode.Ecc.Medium).ToSvgString(QuietZoneModules);
        // The SVG is plain ASCII, so it goes to the log verbatim — a failed scan is diagnosable by pasting it
        // into a viewer, without a device round-trip.
        _log.LogDebug("Encoded QR code ({Length} chars): {Svg}", svg.Length, svg);
        return svg;
    }
}
