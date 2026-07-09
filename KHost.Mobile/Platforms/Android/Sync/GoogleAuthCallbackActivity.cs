using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace KHost.Mobile;

/// <summary>
/// Receives the OAuth redirect at the end of the Google sign-in browser flow and hands it back to
/// <see cref="WebAuthenticator"/>. The <see cref="IntentFilterAttribute.DataScheme"/> below MUST match the reversed
/// client id that <see cref="KHost.Mobile.Services.SyncConfig.GoogleRedirectScheme"/> produces from the real client id.
/// </summary>
/// <remarks>
/// PLACEHOLDER SCHEME: replace <c>com.googleusercontent.apps.REPLACE_WITH_REVERSED_CLIENT_ID</c> with your reversed
/// client id (e.g. client id <c>1234-abcd.apps.googleusercontent.com</c> → scheme
/// <c>com.googleusercontent.apps.1234-abcd</c>). This is a compile-time attribute, so it can't be derived from
/// <c>SyncConfig</c> at runtime — the two must be kept in step by hand. See research/android-sync-setup.md.
/// </remarks>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.REPLACE_WITH_REVERSED_CLIENT_ID")]
public class GoogleAuthCallbackActivity : WebAuthenticatorCallbackActivity
{
}
