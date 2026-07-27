namespace KHost.Mobile.Services;

/// <summary>
/// The device safe-area insets (status / navigation bars and any display cutout) in CSS pixels, for platforms
/// whose WebView does not surface them through CSS <c>env(safe-area-inset-*)</c>. Android's WebView never reports
/// the system bars there, so the activity pushes the measured values in and the layout forwards them to the
/// <c>--kh-inset-*</c> CSS variables; platforms where <c>env()</c> already works (iOS) never push, the values stay
/// zero, and the CSS <c>max()</c> picks <c>env()</c> instead.
/// </summary>
public interface ISafeAreaInsets
{
    /// <summary>Top inset (status bar / cutout) in CSS pixels.</summary>
    double Top { get; }

    /// <summary>Bottom inset (gesture / navigation bar) in CSS pixels.</summary>
    double Bottom { get; }

    /// <summary>Raised when the values change — the first measurement, or a rotation.</summary>
    event EventHandler? Changed;

    /// <summary>Publishes new inset values; no-op (no event) when both are unchanged.</summary>
    void Set(double top, double bottom);
}
