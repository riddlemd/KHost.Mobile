#if ANDROID || IOS
using BarcodeScanning;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Pushes a modal <see cref="QrScanPage"/> over the BlazorWebView; that page owns the camera preview and resolves
/// with the first QR code it reads. Android/iOS only — the native barcode SDKs (ML Kit / Apple Vision) aren't
/// available elsewhere. While the camera is up it registers a top-priority handler with
/// <see cref="IBackButtonService"/> so the hardware back button cancels the scan instead of closing the KaraFun
/// sheet sitting behind it.
/// </remarks>
public sealed class MauiQrScanner(IBackButtonService backButton) : IQrScanner
{
    public async Task<string?> ScanQrCodeAsync(CancellationToken cancellationToken = default)
    {
        // ML Kit / AVFoundation both need camera permission (and VIBRATE on Android); the library's helper requests it.
        if (!await Methods.AskForRequiredPermissionAsync())
            return null;

        var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (hostPage is null)
            return null;

        var navigation = hostPage.Navigation;
        var scanPage = new QrScanPage();

        // Newest handler wins in the registry, so this cancel outranks the open KaraFun sheet's own back handler.
        using var backRegistration = backButton.Register(() => { scanPage.Cancel(); return true; });
        using var cancelOnToken = cancellationToken.Register(scanPage.Cancel);

        await navigation.PushModalAsync(scanPage);
        try
        {
            return await scanPage.ScanAsync();
        }
        finally
        {
            // The page may already be gone if the framework popped it (e.g. its own back handling); only pop ours.
            if (navigation.ModalStack.Contains(scanPage))
                await navigation.PopModalAsync();
        }
    }
}
#endif
