namespace KHost.Mobile.UI.Components.Pages;

public sealed partial class About
{
    private const string PrivacyPolicyUrl = "https://github.com/riddlemd/KHost.Mobile/wiki/Privacy-Policy";

    // Best-effort, like every other outbound link: no network here, and a device with nothing registered to
    // handle the URL simply does nothing.
    private Task OpenPrivacyPolicyAsync() => Links.OpenAsync(PrivacyPolicyUrl);
}
