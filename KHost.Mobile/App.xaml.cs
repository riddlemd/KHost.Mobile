namespace KHost.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // The title is the macOS title bar / Windows caption text, so it uses the product name, not the assembly's.
        var window = new Window(new MainPage()) { Title = "KHost Cue" };

#if MACCATALYST
        // The Catalyst head exists to preview layout changes without an emulator — it is NOT a shipping desktop
        // app. So the window opens at exactly the documented mobile-preview viewport (393 x 852, iPhone 15/16;
        // see DEVELOPMENT.md) rather than some desktop-ish default, and stays resizable so a layout can still be
        // dragged wider to see where it breaks.
        window.Width = 393;
        window.Height = 852;
#endif

        return window;
    }
}
