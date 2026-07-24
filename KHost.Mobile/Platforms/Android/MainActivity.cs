using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Activity;
using AndroidX.Core.View;
using KHost.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
// MAUI's global usings pull in Microsoft.Maui.Controls.View; alias the Android type to disambiguate.
using AView = Android.Views.View;

namespace KHost.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 15+ / API 36 forces edge-to-edge: the Blazor WebView fills the whole window and the app header
        // would draw under the status bar. Android's WebView never populates CSS env(safe-area-inset-*) for the
        // system bars, so publish the measured insets to ISafeAreaInsets instead — MainLayout forwards them into
        // the --kh-inset-* CSS variables once its DOM exists (native can't know when that is; Blazor can). The web
        // side then paints the header's own gradient full-bleed under the bars while padding its content clear of
        // them, so the tint (incl. the per-singer re-tint) extends under the status bar in every theme. The
        // listener re-fires whenever the insets change, which covers rotation.
        var window = Window!;
        var content = window.DecorView!.FindViewById(Android.Resource.Id.Content)!;
        ViewCompat.SetOnApplyWindowInsetsListener(content, new InsetPublisher());
        if (WindowCompat.GetInsetsController(window, window.DecorView!) is { } barsController)
            barsController.AppearanceLightStatusBars = false;   // white status-bar icons on the saturated header

        // Route the hardware/gesture back button through the app's overlay registry: while an in-page overlay
        // (sheet, confirm, menu) is open, back closes it instead of minimizing the app. The callback stays
        // enabled and decides per-press; the service resolve is deferred to press time because the MAUI service
        // provider isn't guaranteed built at OnCreate.
        OnBackPressedDispatcher.AddCallback(this, new OverlayBackPressedCallback(this));
    }

    // Publishes each window-insets pass to ISafeAreaInsets (in CSS px). The service resolve is deferred to fire
    // time, same as the back-button callback below, because the MAUI service provider isn't guaranteed at OnCreate.
    // Uses the SystemBars ∪ DisplayCutout union: on a punch-hole device the cutout can reach deeper than the
    // status-bar inset (Pixel 7: cutout 136px vs statusBars 74px) and the system clock/icons clear the cutout, so
    // the union is what keeps the header content out from under them.
    private sealed class InsetPublisher : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
                return insets!;

            var bars = insets.GetInsets(
                WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout())!;
            var density = view.Resources?.DisplayMetrics?.Density ?? 1f;
            IPlatformApplication.Current?.Services.GetService<ISafeAreaInsets>()
                ?.Set(bars.Top / density, bars.Bottom / density);

            return insets; // unconsumed — the WebView stays full-bleed and the CSS applies the padding
        }
    }

    private sealed class OverlayBackPressedCallback(MainActivity activity) : OnBackPressedCallback(enabled: true)
    {
        // "Press back again to exit": once nothing is left to close, the first back only warns; a second back within
        // this window actually leaves. Uses the monotonic uptime clock so it's immune to wall-clock changes.
        private const long ExitConfirmWindowMillis = 2000;
        private long _lastExitPromptAt;
        private Toast? _exitToast;

        public override void HandleOnBackPressed()
        {
            var backButton = IPlatformApplication.Current?.Services.GetService<IBackButtonService>();
            if (backButton is not null && backButton.HandleBack())
                return; // an overlay consumed the press — never counts toward the exit sequence

            // Nothing open to close. Require a confirming second back within the window before leaving.
            var now = SystemClock.ElapsedRealtime();
            if (now - _lastExitPromptAt <= ExitConfirmWindowMillis)
            {
                _exitToast?.Cancel();
                // Perform the platform default (root activity → minimize) by disabling this callback and
                // re-dispatching so the framework default runs, then re-arm for next time the app is resumed.
                Enabled = false;
                activity.OnBackPressedDispatcher.OnBackPressed();
                Enabled = true;
                return;
            }

            _lastExitPromptAt = now;
            ShowExitToast();
        }

        // A standard Toast.MakeText picks up the system-added app icon on Android 12+; a custom-view toast renders
        // just our text (and still shows because the app is foreground when back is pressed).
        private void ShowExitToast()
        {
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            int Dp(float value) => (int)(value * density + 0.5f);

            var text = new TextView(activity);
            text.Text = "Press back again to exit";
            text.SetTextColor(Android.Graphics.Color.White);
            text.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
            text.SetPadding(Dp(20), Dp(12), Dp(20), Dp(12));

            var background = new Android.Graphics.Drawables.GradientDrawable();
            background.SetColor(Android.Graphics.Color.Argb(235, 32, 32, 32));
            background.SetCornerRadius(Dp(24));
            text.Background = background;

            _exitToast?.Cancel();
#pragma warning disable CS0618, CA1422 // Custom-view toasts are deprecated on API 30+ but still render for a foreground app, which we always are here.
            _exitToast = new Toast(activity) { Duration = ToastLength.Short };
            _exitToast.View = text;
#pragma warning restore CS0618, CA1422
            _exitToast.Show();
        }
    }
}
