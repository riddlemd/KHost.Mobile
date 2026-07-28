using Microsoft.Maui.Devices;
using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Services;

/// <inheritdoc />
/// <remarks>
/// Wraps MAUI Essentials' <see cref="HapticFeedback"/>. Deliberately used instead of the browser's
/// <c>navigator.vibrate</c> from JS: WKWebView doesn't implement that API at all, so an iOS long-press
/// would land with no feedback.
/// </remarks>
public sealed class MauiHaptics(IAppSettings settings) : IHaptics
{
    /// <inheritdoc />
    public void LongPress()
    {
        // Gated here rather than at each gesture handler so a new caller can't ship ignoring the setting.
        if (!settings.HapticsEnabled)
            return;

        // Unsupported hardware throws rather than no-opping, and a missing tick must never break the gesture
        // that triggered it.
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); }
        catch (FeatureNotSupportedException) { }
        catch (Exception) { }
    }
}
