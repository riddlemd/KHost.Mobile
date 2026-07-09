using System.Text.Json;
using KHost.Mobile.Models;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <summary>
/// A file-backed stand-in "cloud" for the Windows iteration head — the same role <c>GoogleDriveBackend</c> plays on
/// Android and <c>AppleCloudBackend</c> will play on iOS, but the "remote" is just <c>fake-cloud.json</c> next to the
/// real store. This lets the entire coordinator + merge path run and be driven end-to-end without a Google account:
/// seed/inspect the file from outside the app to simulate another device, and watch the reconcile land in the UI.
/// Windows is a dev-only head (ship targets are iOS/Android), so wiring this there is harmless. Always
/// <see cref="SyncAuthState.SignedIn"/>; optimistic concurrency via a monotonically bumped integer Version.
/// </summary>
public sealed class FakeSyncBackend : ISyncBackend
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _cloudPath = Path.Combine(FileSystem.AppDataDirectory, "fake-cloud.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Test hook: set KH_FAKE_SYNC_PULL_DELAY_MS to simulate real cloud latency and open the concurrent-edit race
    // window (a real Drive pull takes seconds; the file-backed fake is sub-millisecond). Default 0 = no delay.
    private static readonly int PullDelayMs =
        int.TryParse(Environment.GetEnvironmentVariable("KH_FAKE_SYNC_PULL_DELAY_MS"), out var ms) ? ms : 0;

    private sealed record CloudFile(string Version, List<SongListItem> Items);

    public string Name => "Test Cloud";
    public SyncAuthState AuthState => SyncAuthState.SignedIn;
    public string? AccountLabel => "windows-dev@fake.cloud";

    public event EventHandler? AuthStateChanged { add { } remove { } }

    public Task<SyncAuthState> ConnectAsync(CancellationToken ct = default) => Task.FromResult(SyncAuthState.SignedIn);
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<SyncSnapshot?> PullAsync(CancellationToken ct = default)
    {
        if (PullDelayMs > 0)
            await Task.Delay(PullDelayMs, ct);   // simulate network latency to expose the concurrent-edit race
        await _gate.WaitAsync(ct);
        try
        {
            var cloud = await ReadAsync();
            return cloud is null ? null : new SyncSnapshot(cloud.Items, cloud.Version);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SyncPushResult> PushAsync(SyncSnapshot merged, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cloud = await ReadAsync();
            var currentVersion = cloud?.Version;   // null when nothing stored yet
            if (currentVersion != merged.Version)
                return new SyncPushResult(SyncPushOutcome.Conflict, null);

            var nextVersion = ((cloud is null ? 0 : int.Parse(cloud.Version)) + 1).ToString();
            var stored = new CloudFile(nextVersion, merged.Items.ToList());
            await using (var stream = File.Create(_cloudPath))
                await JsonSerializer.SerializeAsync(stream, stored, Options, ct);

            return new SyncPushResult(SyncPushOutcome.Applied, new SyncSnapshot(stored.Items, nextVersion));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CloudFile?> ReadAsync()
    {
        if (!File.Exists(_cloudPath))
            return null;
        try
        {
            await using var stream = File.OpenRead(_cloudPath);
            return await JsonSerializer.DeserializeAsync<CloudFile>(stream);
        }
        catch (JsonException)
        {
            return null;   // treat a hand-seeded-but-malformed cloud as empty rather than crashing the sync
        }
    }
}
