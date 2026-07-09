namespace KHost.Mobile.Services;

/// <summary>
/// Static configuration for the cloud-sync backends. The Google OAuth <b>client id is a public value</b> (it ships in
/// the app and is protected by PKCE, not by secrecy), so it lives here rather than in user-secrets — but a real one is
/// deliberately NOT committed. Until <see cref="GoogleClientId"/> is set to a real client id, the Android backend
/// reports <see cref="SyncAuthState.NotConfigured"/> and the app runs purely local (no sync affordance in the menu).
/// </summary>
/// <remarks>
/// To enable Google Drive sync (see research/android-sync-setup.md for the full checklist):
/// <list type="number">
///   <item>Create a Google Cloud project, enable the Drive API, configure the OAuth consent screen.</item>
///   <item>Create an OAuth client id for the flow and paste it into <see cref="GoogleClientId"/>.</item>
///   <item>Register the reversed-client-id redirect scheme on the Android callback activity (it derives from the id).</item>
/// </list>
/// </remarks>
public static class SyncConfig
{
    /// <summary>The sentinel meaning "not set". Kept as a constant so <see cref="IsGoogleConfigured"/> is a simple ==.</summary>
    public const string GoogleClientIdPlaceholder = "YOUR_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com";

    /// <summary>Google OAuth 2.0 client id for the installed-app PKCE flow. Public by design; replace the placeholder
    /// with a real id to turn on Drive sync. Do not commit a real value.</summary>
    public static string GoogleClientId { get; set; } = GoogleClientIdPlaceholder;

    /// <summary>Scopes requested at sign-in: a hidden per-app Drive folder (<c>drive.appdata</c>) plus the minimum to
    /// show the signed-in account's email in the menu.</summary>
    public const string GoogleScopes = "https://www.googleapis.com/auth/drive.appdata openid email";

    /// <summary>True once a real client id has been supplied. Backends short-circuit to NotConfigured when false.</summary>
    public static bool IsGoogleConfigured =>
        !string.IsNullOrWhiteSpace(GoogleClientId) && GoogleClientId != GoogleClientIdPlaceholder;

    /// <summary>The custom-scheme redirect used by the WebAuthenticator flow: the reversed client id (Google's
    /// installed-app convention), e.g. <c>com.googleusercontent.apps.1234-abcd</c>. Must match the scheme registered
    /// on the Android callback activity's intent-filter.</summary>
    public static string GoogleRedirectScheme
    {
        get
        {
            // "1234-abcd.apps.googleusercontent.com" -> "com.googleusercontent.apps.1234-abcd"
            const string suffix = ".apps.googleusercontent.com";
            var id = GoogleClientId;
            var bare = id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? id[..^suffix.Length]
                : id;
            return $"com.googleusercontent.apps.{bare}";
        }
    }

    /// <summary>Full redirect URI: the reversed-client-id scheme plus a fixed path. WebAuthenticator matches on this.</summary>
    public static string GoogleRedirectUri => $"{GoogleRedirectScheme}:/oauth2redirect";
}
