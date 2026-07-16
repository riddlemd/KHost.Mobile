#if !ANDROID && !IOS
namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Stub for platforms without a native barcode SDK (e.g. the Windows dev head). The scanner UI never offers a scan
/// button there, but the KaraFun sheet still resolves <see cref="IQrScanner"/> via DI on every platform — so this
/// keeps that resolvable and returns <c>null</c> (nothing scanned).
/// </remarks>
public sealed class UnsupportedQrScanner : IQrScanner
{
    public Task<string?> ScanQrCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
#endif
