using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// Google OAuth 2.0 for an installed app: the <b>authorization-code + PKCE</b> flow driven by MAUI
/// <see cref="WebAuthenticator"/> (no client secret, no Play Services dependency — the same shape reused on iOS later).
/// Owns the whole token lifecycle: interactive sign-in, refresh-token persistence in <see cref="SecureStorage"/>, and
/// minting a fresh access token on demand. <see cref="GoogleDriveBackend"/> layers Drive I/O on top of
/// <see cref="GetValidAccessTokenAsync"/>.
/// </summary>
/// <remarks>
/// UNVERIFIED at build time: the live flow needs a real <see cref="SyncConfig.GoogleClientId"/>, a matching redirect
/// scheme registered on <c>GoogleAuthCallbackActivity</c>, and a real Google login. This compiles and is
/// correct-by-inspection; the round-trip is on the smoke-test checklist (research/android-sync-setup.md).
/// </remarks>
public sealed class GoogleOAuthClient
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RefreshTokenKey = "google_refresh_token";
    private const string AccountEmailKey = "google_account_email";

    private readonly HttpClient _http;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;
    private string? _refreshToken;
    private bool _loaded;

    public GoogleOAuthClient(HttpClient http) => _http = http;

    /// <summary>The signed-in Google account email (from the id_token), for display. Null when signed out.</summary>
    public string? AccountEmail { get; private set; }

    /// <summary>Whether a refresh token is on hand (a prior sign-in that survives app restarts).</summary>
    public async Task<bool> IsSignedInAsync()
    {
        await EnsureLoadedAsync();
        return _refreshToken is not null;
    }

    /// <summary>Run the interactive consent + code exchange, persisting the refresh token. Returns true on success.</summary>
    public async Task<bool> SignInAsync(CancellationToken ct = default)
    {
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = CreateCodeVerifier();   // reuse the CSPRNG helper for an opaque state value

        var authUrl =
            $"{AuthEndpoint}?response_type=code" +
            $"&client_id={Uri.EscapeDataString(SyncConfig.GoogleClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(SyncConfig.GoogleRedirectUri)}" +
            $"&scope={Uri.EscapeDataString(SyncConfig.GoogleScopes)}" +
            $"&code_challenge={challenge}&code_challenge_method=S256" +
            $"&state={state}&access_type=offline&prompt=consent";

        WebAuthenticatorResult authResult;
        try
        {
            authResult = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = new Uri(authUrl),
                CallbackUrl = new Uri(SyncConfig.GoogleRedirectUri),
                PrefersEphemeralWebBrowserSession = false,
            });
        }
        catch (TaskCanceledException)
        {
            return false;   // user dismissed the consent screen
        }

        if (!authResult.Properties.TryGetValue("state", out var returnedState) || returnedState != state)
            return false;   // CSRF guard
        if (!authResult.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            return false;

        var form = new Dictionary<string, string>
        {
            ["client_id"] = SyncConfig.GoogleClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = SyncConfig.GoogleRedirectUri,
            ["grant_type"] = "authorization_code",
        };

        using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode)
            return false;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString();
        _accessTokenExpiry = DateTimeOffset.Now.AddSeconds(root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3000);
        _refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : _refreshToken;
        AccountEmail = root.TryGetProperty("id_token", out var idt) ? ReadEmailClaim(idt.GetString()) : AccountEmail;

        if (_refreshToken is not null)
            await SecureStorage.Default.SetAsync(RefreshTokenKey, _refreshToken);
        if (AccountEmail is not null)
            await SecureStorage.Default.SetAsync(AccountEmailKey, AccountEmail);

        _loaded = true;
        return _accessToken is not null;
    }

    /// <summary>Forget the account: drop the in-memory tokens and the stored refresh token.</summary>
    public Task SignOutAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        AccountEmail = null;
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(AccountEmailKey);
        return Task.CompletedTask;
    }

    /// <summary>A non-expired access token, refreshing via the stored refresh token when needed. Null if signed out or
    /// the refresh fails (e.g. revoked consent) — the caller should surface a re-sign-in.</summary>
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        if (_refreshToken is null)
            return null;
        if (_accessToken is not null && DateTimeOffset.Now < _accessTokenExpiry.AddMinutes(-2))
            return _accessToken;   // still good (2-min safety margin)

        var form = new Dictionary<string, string>
        {
            ["client_id"] = SyncConfig.GoogleClientId,
            ["refresh_token"] = _refreshToken,
            ["grant_type"] = "refresh_token",
        };

        using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode)
        {
            // Refresh token no longer valid (revoked / expired): force a fresh sign-in next time.
            if ((int)resp.StatusCode is 400 or 401)
                await SignOutAsync();
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        _accessToken = root.GetProperty("access_token").GetString();
        _accessTokenExpiry = DateTimeOffset.Now.AddSeconds(root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3000);
        return _accessToken;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        AccountEmail = await SecureStorage.Default.GetAsync(AccountEmailKey);
        _loaded = true;
    }

    // ---- PKCE + JWT helpers ------------------------------------------------

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Pull the <c>email</c> claim out of a Google id_token (a JWT) without validating the signature — it came
    /// straight from Google's token endpoint over TLS, and it's only used for a display label.</summary>
    private static string? ReadEmailClaim(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
            return null;
        var parts = idToken.Split('.');
        if (parts.Length < 2)
            return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("email", out var em) ? em.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
