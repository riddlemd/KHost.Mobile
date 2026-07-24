namespace KHost.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

#if ANDROID
        // Kill the native overscroll stretch. With the WebView edge-to-edge, a fling past the end of the page
        // drags the sticky app header up into the status-bar strip for a beat before springing back. The stretch
        // is applied by the platform WebView's overscroll plumbing and ignores CSS overscroll-behavior (verified
        // on-device: computed style "none", stretch still ran), so it has to be switched off on the widget itself.
        blazorWebView.BlazorWebViewInitialized += (_, e) =>
            e.WebView.OverScrollMode = Android.Views.OverScrollMode.Never;
#endif
    }
}
