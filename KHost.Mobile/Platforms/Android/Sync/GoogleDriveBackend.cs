using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// Android <see cref="ISyncBackend"/> over the user's own Google Drive — the whole song list lives as a single JSON
/// blob in the hidden per-app <c>appDataFolder</c> (the <c>drive.appdata</c> scope), invisible in the user's Drive UI
/// and never touching their other files. A dumb transport, exactly as the interface intends: it authenticates (via
/// <see cref="GoogleOAuthClient"/>), pulls the whole blob, and pushes the whole blob. All reconcile logic is upstream
/// in <see cref="SyncMerge"/>. Optimistic concurrency uses the Drive file's <c>headRevisionId</c> as the opaque
/// snapshot Version, so a push made against a stale revision is rejected as a <see cref="SyncPushOutcome.Conflict"/>.
/// </summary>
/// <remarks>
/// UNVERIFIED at build time — the Drive round-trip needs a real OAuth client id + Google login (see
/// research/android-sync-setup.md). Compiles for the Android head and is correct-by-inspection only.
/// </remarks>
public sealed class GoogleDriveBackend : ISyncBackend
{
    private const string FileName = "khost-cue-songs.json";
    private const string FilesApi = "https://www.googleapis.com/drive/v3/files";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly HttpClient _http;
    private readonly GoogleOAuthClient _oauth;
    private bool _signedIn;

    public GoogleDriveBackend(HttpClient http, GoogleOAuthClient oauth)
    {
        _http = http;
        _oauth = oauth;
        _ = InitAsync();   // resolve persisted sign-in state, then let the coordinator know
    }

    public string Name => "Google Drive";

    public SyncAuthState AuthState =>
        !SyncConfig.IsGoogleConfigured ? SyncAuthState.NotConfigured
        : _signedIn ? SyncAuthState.SignedIn
        : SyncAuthState.SignedOut;

    public string? AccountLabel => _oauth.AccountEmail;

    public event EventHandler? AuthStateChanged;

    private async Task InitAsync()
    {
        if (!SyncConfig.IsGoogleConfigured)
            return;
        _signedIn = await _oauth.IsSignedInAsync();
        if (_signedIn)
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<SyncAuthState> ConnectAsync(CancellationToken ct = default)
    {
        if (!SyncConfig.IsGoogleConfigured)
            return SyncAuthState.NotConfigured;

        _signedIn = await _oauth.SignInAsync(ct);
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return AuthState;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _oauth.SignOutAsync();
        _signedIn = false;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<SyncSnapshot?> PullAsync(CancellationToken ct = default)
    {
        var token = await RequireTokenAsync(ct);
        var file = await FindFileAsync(token, ct);
        if (file is null)
            return null;   // nothing stored yet — first-ever sync

        using var req = Authed(HttpMethod.Get, $"{FilesApi}/{file.Value.Id}?alt=media", token);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var items = await JsonSerializer.DeserializeAsync<List<SongListItem>>(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct) ?? [];
        return new SyncSnapshot(items, file.Value.HeadRevisionId);
    }

    public async Task<SyncPushResult> PushAsync(SyncSnapshot merged, CancellationToken ct = default)
    {
        var token = await RequireTokenAsync(ct);
        var file = await FindFileAsync(token, ct);

        // Optimistic concurrency: reject if the remote moved past the revision this push was merged against.
        var currentVersion = file?.HeadRevisionId;
        if (currentVersion != merged.Version)
            return new SyncPushResult(SyncPushOutcome.Conflict, null);

        var body = JsonSerializer.Serialize(merged.Items, Json);
        var stored = file is null
            ? await CreateFileAsync(token, body, ct)
            : await UpdateFileAsync(token, file.Value.Id, body, ct);

        return new SyncPushResult(SyncPushOutcome.Applied, new SyncSnapshot(merged.Items, stored));
    }

    // ---- Drive REST plumbing ----------------------------------------------

    private readonly record struct DriveFile(string Id, string? HeadRevisionId);

    private async Task<DriveFile?> FindFileAsync(string token, CancellationToken ct)
    {
        var url = $"{FilesApi}?spaces=appDataFolder" +
                  $"&q={Uri.EscapeDataString($"name = '{FileName}' and trashed = false")}" +
                  "&fields=files(id,headRevisionId)&pageSize=1";
        using var req = Authed(HttpMethod.Get, url, token);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var files = doc.RootElement.GetProperty("files");
        if (files.GetArrayLength() == 0)
            return null;

        var f = files[0];
        return new DriveFile(
            f.GetProperty("id").GetString()!,
            f.TryGetProperty("headRevisionId", out var h) ? h.GetString() : null);
    }

    private async Task<string?> CreateFileAsync(string token, string body, CancellationToken ct)
    {
        // multipart/related: part 1 = metadata (name + appDataFolder parent), part 2 = the JSON media.
        var metadata = JsonSerializer.Serialize(new { name = FileName, parents = new[] { "appDataFolder" } });
        var content = new MultipartContent("related")
        {
            new StringContent(metadata, Encoding.UTF8, "application/json"),
            new StringContent(body, Encoding.UTF8, "application/json"),
        };

        using var req = Authed(HttpMethod.Post, $"{UploadApi}?uploadType=multipart&fields=headRevisionId", token);
        req.Content = content;
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await ReadHeadRevisionAsync(resp, ct);
    }

    private async Task<string?> UpdateFileAsync(string token, string id, string body, CancellationToken ct)
    {
        using var req = Authed(HttpMethod.Patch, $"{UploadApi}/{id}?uploadType=media&fields=headRevisionId", token);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await ReadHeadRevisionAsync(resp, ct);
    }

    private static async Task<string?> ReadHeadRevisionAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("headRevisionId", out var h) ? h.GetString() : null;
    }

    private async Task<string> RequireTokenAsync(CancellationToken ct)
    {
        var token = await _oauth.GetValidAccessTokenAsync(ct);
        if (token is null)
        {
            // Session died (revoked / expired refresh). Reflect it so the menu offers a re-sign-in.
            if (_signedIn)
            {
                _signedIn = false;
                AuthStateChanged?.Invoke(this, EventArgs.Empty);
            }
            throw new InvalidOperationException("Google Drive session expired; sign in again.");
        }
        return token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }
}
