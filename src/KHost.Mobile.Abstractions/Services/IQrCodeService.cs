namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Renders text as a scannable QR code. Local and offline — encoding never leaves the device.
/// </summary>
/// <remarks>
/// The contract is deliberately free of any encoder's types so the backend can be replaced without touching a
/// caller. Implementations are expected to be pure and thread-safe.
/// </remarks>
public interface IQrCodeService
{
    /// <summary>
    /// Encodes <paramref name="text"/> as an <c>&lt;svg&gt;</c> element sized in module units, so the caller
    /// scales it with CSS rather than re-encoding at a different size.
    /// </summary>
    /// <param name="text">What the code should carry — typically a URL.</param>
    /// <returns>SVG markup for the code, including its quiet zone.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or whitespace.</exception>
    string ToSvg(string text);
}
