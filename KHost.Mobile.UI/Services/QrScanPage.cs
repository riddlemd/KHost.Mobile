#if ANDROID || IOS
using BarcodeScanning;

namespace KHost.Mobile.Services;

/// <summary>
/// A full-screen modal camera page that resolves with the first QR code it reads, or <c>null</c> if cancelled.
/// Built in code (no XAML) so it lives beside <see cref="MauiQrScanner"/>; symbology is pinned to QR so a stray
/// 1D barcode in frame can't resolve it.
/// </summary>
internal sealed class QrScanPage : ContentPage
{
    private readonly CameraView _camera;
    private readonly TaskCompletionSource<string?> _result = new();
    private int _resolved;

    public QrScanPage()
    {
        BackgroundColor = Colors.Black;

        _camera = new CameraView
        {
            CameraEnabled = false,                      // turned on in OnAppearing, per the library's guidance
            CameraFacing = CameraFacing.Back,
            BarcodeSymbologies = BarcodeFormats.QRCode,
            VibrationOnDetected = true,
        };
        _camera.OnDetectionFinished += OnDetectionFinished;

        var instructions = new Label
        {
            Text = "Point the camera at the venue's KaraFun QR code",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(16, 12),
            BackgroundColor = Color.FromRgba(0, 0, 0, 160),
            Margin = new Thickness(24),
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Center,
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Margin = new Thickness(24, 32),
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Center,
        };
        cancel.Clicked += (_, _) => Cancel();

        Content = new Grid
        {
            Children = { _camera, instructions, cancel },
        };
    }

    /// <summary>Completes when a QR code is read or the scan is cancelled.</summary>
    public Task<string?> ScanAsync() => _result.Task;

    /// <summary>Cancels the scan (resolves with <c>null</c>). Safe to call more than once.</summary>
    public void Cancel() => Resolve(null);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _camera.CameraEnabled = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _camera.CameraEnabled = false;
        // Safety net: if the page is dismissed by any path that didn't resolve first, treat it as a cancel so the
        // awaiting scan never hangs.
        Resolve(null);
    }

    // Hardware back on this page cancels the scan. Returning true stops the framework from also popping it — the
    // scanner service owns the pop.
    protected override bool OnBackButtonPressed()
    {
        Cancel();
        return true;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var qr = e.BarcodeResults.FirstOrDefault(b => b.BarcodeFormat == BarcodeFormats.QRCode);
        if (qr is not null && !string.IsNullOrWhiteSpace(qr.DisplayValue))
            Resolve(qr.DisplayValue);
    }

    // OnDetectionFinished can fire repeatedly (and off the UI thread), so resolve exactly once and shut the camera
    // down on the UI thread.
    private void Resolve(string? value)
    {
        if (Interlocked.Exchange(ref _resolved, 1) != 0)
            return;
        MainThread.BeginInvokeOnMainThread(() => _camera.CameraEnabled = false);
        _result.TrySetResult(value);
    }
}
#endif
