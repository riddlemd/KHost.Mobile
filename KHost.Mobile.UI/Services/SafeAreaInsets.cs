namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>A plain value holder: the Android activity's inset listener writes, the layout reads. Values only
/// ever arrive from the UI thread (a window-insets pass), so no synchronization is needed.</remarks>
public sealed class SafeAreaInsets : ISafeAreaInsets
{
    /// <inheritdoc />
    public double Top { get; private set; }

    /// <inheritdoc />
    public double Bottom { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Set(double top, double bottom)
    {
        if (top == Top && bottom == Bottom)
            return;

        Top = top;
        Bottom = bottom;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
