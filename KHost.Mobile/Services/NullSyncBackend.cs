namespace KHost.Mobile.Services;

/// <summary>
/// The "sync isn't available here" backend. Ships on any head without a real cloud implementation (today: iOS until
/// the CloudKit backend lands, and any misconfigured build). Permanently <see cref="SyncAuthState.NotConfigured"/>, so
/// <see cref="SyncCoordinator"/> short-circuits every trigger and the app runs purely local — no menu sync affordance.
/// </summary>
public sealed class NullSyncBackend : ISyncBackend
{
    public string Name => "Sync";
    public SyncAuthState AuthState => SyncAuthState.NotConfigured;
    public string? AccountLabel => null;

    public event EventHandler? AuthStateChanged { add { } remove { } }

    public Task<SyncAuthState> ConnectAsync(CancellationToken ct = default) => Task.FromResult(SyncAuthState.NotConfigured);
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<SyncSnapshot?> PullAsync(CancellationToken ct = default) => Task.FromResult<SyncSnapshot?>(null);
    public Task<SyncPushResult> PushAsync(SyncSnapshot merged, CancellationToken ct = default)
        => Task.FromResult(new SyncPushResult(SyncPushOutcome.Applied, merged));
}
