#if MACCATALYST
using Microsoft.Maui.Primitives;
#endif

namespace KHost.Mobile;

public partial class App : Application
{
#if MACCATALYST
    // The mobile-preview viewport from DEVELOPMENT.md. It sizes the WEB VIEW, not the window (which is a title
    // bar taller).
    private const double PreviewViewportWidth = 393;
    private const double PreviewViewportHeight = 852;
#endif

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // The title is the macOS title bar / Windows caption text, so it uses the product name, not the assembly's.
        var window = new Window(new MainPage()) { Title = "KHost Cue" };

#if MACCATALYST
        PinToMobilePreviewViewport(window);
#endif

        return window;
    }

#if MACCATALYST
    /// <summary>
    /// Opens the Catalyst window at the documented mobile-preview viewport, then releases it to the user to resize.
    /// </summary>
    /// <remarks>
    /// Don't simplify this to <see cref="Window.Width"/>/<see cref="Window.Height"/>: MAUI only issues the macOS
    /// geometry request when X, Y, Width and Height are ALL set, and that request is then clamped to the screen's
    /// visible frame (the Dock is enough to shrink it). Size restrictions aren't clamped, so min = max is the only
    /// way to land an exact size.
    /// </remarks>
    private static void PinToMobilePreviewViewport(Window window)
    {
        window.MinimumWidth = PreviewViewportWidth;
        window.MaximumWidth = PreviewViewportWidth;
        window.MinimumHeight = PreviewViewportHeight;
        window.MaximumHeight = PreviewViewportHeight;

        if (window.Page is not { } page)
        {
            return;
        }

        EventHandler? growByTitleBar = null;
        growByTitleBar = (_, _) =>
        {
            // macOS takes a title bar's worth of the window back as a safe-area inset, so the page (= the web view)
            // comes up short by it. Measured rather than hard-coded: the inset reads 0 until the first layout pass
            // settles, and 0 / anything too big to be a title bar means there's nothing useful to correct.
            var shortfall = PreviewViewportHeight - page.Height;
            if (shortfall is <= 0 or >= 200)
            {
                return;
            }

            page.SizeChanged -= growByTitleBar;
            window.MinimumHeight = PreviewViewportHeight + shortfall;
            window.MaximumHeight = PreviewViewportHeight + shortfall;

            // Release the pin so the window stays resizable. Deferred: the pin is what drives the resize, so
            // dropping it in the same pass leaves the window at its old size.
            window.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                window.MinimumWidth = Dimension.Minimum;
                window.MaximumWidth = Dimension.Maximum;
                window.MinimumHeight = Dimension.Minimum;
                window.MaximumHeight = Dimension.Maximum;
            });
        };

        page.SizeChanged += growByTitleBar;
    }
#endif
}
