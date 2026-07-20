using Microsoft.Maui.Devices;

namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Wraps MAUI Essentials' <see cref="HapticFeedback"/>. Deliberately used instead of the browser's
/// <c>navigator.vibrate</c> from JS: WKWebView doesn't implement that API at all, so an iOS long-press
/// would land with no feedback.
/// </remarks>
public sealed class MauiHaptics : IHaptics
{
    /// <inheritdoc />
    public void LongPress()
    {
        // Unsupported hardware throws rather than no-opping, and a missing tick must never break the gesture
        // that triggered it.
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); }
        catch (FeatureNotSupportedException) { }
        catch (Exception) { }
    }
}
