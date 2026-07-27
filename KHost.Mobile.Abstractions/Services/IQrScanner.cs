namespace KHost.Mobile.Services;

/// <summary>
/// Scans a QR code with the device camera. Backed natively on Android/iOS (ML Kit / Apple Vision); a no-op on
/// platforms without a scanner (e.g. the Windows dev head), where it returns <c>null</c>.
/// </summary>
public interface IQrScanner
{
    /// <summary>
    /// Opens the camera to scan a single QR code and returns its decoded text — or <c>null</c> if the user
    /// cancelled, denied camera permission, or the platform has no scanner.
    /// </summary>
    Task<string?> ScanQrCodeAsync(CancellationToken cancellationToken = default);
}
