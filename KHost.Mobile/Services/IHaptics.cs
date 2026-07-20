namespace KHost.Mobile.Services;

/// <summary>
/// Short device haptics for gestures that complete without a visual transition of their own — today the
/// press-and-hold that sets the active venue/singer, where the tick is what tells the user the hold took.
/// </summary>
/// <remarks>
/// Named <c>IHaptics</c> rather than <c>IHapticFeedback</c> to stay clear of MAUI Essentials'
/// <see cref="Microsoft.Maui.Devices.IHapticFeedback"/>, which the MAUI global usings pull into scope.
/// </remarks>
public interface IHaptics
{
    /// <summary>
    /// Ticks the device's long-press haptic. Best-effort: silently does nothing where the platform has no
    /// haptics, the hardware is absent, or the user has switched them off system-wide.
    /// </summary>
    void LongPress();
}
