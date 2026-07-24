#if MACCATALYST
using Microsoft.Maui.Primitives;
#endif

namespace KHost.Mobile;

public partial class App : Application
{
#if MACCATALYST
    // The documented mobile-preview viewport — 393 x 852 (iPhone 15/16), see DEVELOPMENT.md. This is the size of
    // the WEB VIEW, which is what the screenshot grid and the CSS breakpoints are measured against, not of the
    // macOS window — which ends up a title bar taller.
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
    /// The Catalyst head exists to preview layout changes without an emulator — it is NOT a shipping desktop app —
    /// so it opens at the phone viewport rather than some desktop-ish default. Two Catalyst-isms make that harder
    /// than setting <see cref="Window.Width"/>:
    /// <list type="bullet">
    /// <item><description>
    /// Width/Height alone do nothing. MAUI's <c>WindowExtensions.UpdateCoordinates</c> issues the macOS geometry
    /// request only when X, Y, Width AND Height are all non-NaN, so setting just the size is a silent no-op and the
    /// window opens at Catalyst's 1024 x 768 default — a desktop-wide layout, exactly what this head is not for.
    /// Supplying a position too still isn't enough: that request is clamped to the screen's visible frame, so the
    /// Dock alone is enough to quietly hand back a viewport shorter than asked for. The scene's size restrictions
    /// (<see cref="Window.MinimumHeight"/> and friends) are not clamped, so pinning min = max is the only way to
    /// land the exact size — and the pin is then released so the window stays resizable.
    /// </description></item>
    /// <item><description>
    /// The size applies to the window FRAME, of which macOS claims a slice back as a safe-area inset for the title
    /// bar — leaving the web view a title bar short of the viewport. The inset reads 0 until the first layout pass
    /// settles, and a hard-coded title-bar height would rot across macOS versions, so it is measured instead.
    /// </description></item>
    /// </list>
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
            // The page IS the web view area, so its height is the viewport the CSS sees, and whatever it comes up
            // short is exactly the chrome to grow the window by.
            var shortfall = PreviewViewportHeight - page.Height;

            // Ignore the layout pass before the inset is applied (the page is still full-frame height, so nothing
            // is missing yet) and any shortfall too large to be a title bar — on a display too small to hold the
            // preview, growing the window wouldn't fix it.
            if (shortfall is <= 0 or >= 200)
            {
                return;
            }

            page.SizeChanged -= growByTitleBar;
            window.MinimumHeight = PreviewViewportHeight + shortfall;
            window.MaximumHeight = PreviewViewportHeight + shortfall;

            // Then hand the window back: this head is for dragging a layout wider to find where it breaks, which a
            // pinned window can't do. Deferred rather than immediate because the pin is what drives the resize —
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
