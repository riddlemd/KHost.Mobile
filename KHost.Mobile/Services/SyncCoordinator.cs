using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>Where the last sync attempt landed — drives the header-menu status line.</summary>
public enum SyncResult
{
    /// <summary>Never attempted since launch.</summary>
    Idle,

    /// <summary>Synced; local and cloud now agree.</summary>
    Ok,

    /// <summary>Nothing to do — already converged.</summary>
    UpToDate,

    /// <summary>No account connected, so nothing ran (Android before Google Sign-In; iOS with iCloud off).</summary>
    NotSignedIn,

    /// <summary>The backend isn't available on this build/device (e.g. Windows/iOS before iCloud lands).</summary>
    NotConfigured,

    /// <summary>Network/cloud error; will retry on the next trigger.</summary>
    Failed,
}

/// <summary>
/// The reconcile engine that sits between the UI's local store and a platform <see cref="ISyncBackend"/>. It owns the
/// whole sync lifecycle — <b>pull → <see cref="SyncMerge"/> → apply-local → push</b> — so the backends stay dumb
/// transports and every platform reconciles identically. Sync fires on launch, debounced after each local edit, and on
/// demand from the menu. All merge/reconcile logic lives in <see cref="SyncMerge"/>; this class is the scheduler and
/// the single-flight guard around it.
/// </summary>
public sealed class SyncCoordinator : IDisposable
{
    private const int DebounceMs = 1500;
    private const int MaxPushRetries = 3;

    private readonly ILocalSongStore _local;
    private readonly ISyncBackend _backend;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private CancellationTokenSource? _debounceCts;
    private bool _suppressLocalChanged;   // true while we're applying a merge result, so our own write doesn't re-trigger
    private bool _started;

    public SyncCoordinator(ILocalSongStore local, ISyncBackend backend)
    {
        _local = local;
        _backend = backend;
        _local.Changed += OnLocalChanged;
        _backend.AuthStateChanged += OnAuthStateChanged;
    }

    /// <summary>Raised (on a threadpool context) whenever sync status or auth state changes, so the UI can refresh.</summary>
    public event EventHandler? StateChanged;

    public string BackendName => _backend.Name;
    public SyncAuthState AuthState => _backend.AuthState;
    public string? AccountLabel => _backend.AccountLabel;
    public bool IsSyncing { get; private set; }
    public SyncResult LastResult { get; private set; } = SyncResult.Idle;
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Kick off the first sync after the UI is up. Idempotent — safe to call from every component that injects
    /// the coordinator; only the first call does anything.</summary>
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _ = SyncNowAsync();
    }

    /// <summary>Connect the cloud account (Android: launch Google Sign-In), then sync immediately on success.</summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var state = await _backend.ConnectAsync(ct);
        if (state == SyncAuthState.SignedIn)
            await SyncNowAsync(ct);
        RaiseStateChanged();
    }

    /// <summary>Sign out of the cloud account. Local data is untouched.</summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _backend.DisconnectAsync(ct);
        RaiseStateChanged();
    }

    /// <summary>Run one full reconcile now. Single-flight: a second call while one is in progress waits its turn rather
    /// than interleaving. Never throws — failures land in <see cref="LastResult"/>/<see cref="LastError"/>.</summary>
    public async Task<SyncResult> SyncNowAsync(CancellationToken ct = default)
    {
        if (_backend.AuthState == SyncAuthState.NotConfigured)
            return Finish(SyncResult.NotConfigured);
        if (_backend.AuthState == SyncAuthState.SignedOut)
            return Finish(SyncResult.NotSignedIn);

        await _syncGate.WaitAsync(ct);
        IsSyncing = true;
        RaiseStateChanged();
        try
        {
            for (var attempt = 0; attempt <= MaxPushRetries; attempt++)
            {
                var localRaw = await _local.GetRawAsync();
                var remote = await _backend.PullAsync(ct);

                var merged = SyncMerge.Merge(localRaw, remote?.Items);
                merged = SyncMerge.Compact(merged, DateTimeOffset.Now);

                // Push first: only after the cloud accepts do we adopt the merged snapshot locally, so a rejected
                // push (someone else wrote concurrently) never leaves local ahead of cloud.
                var pushNeeded = remote is null || SyncMerge.Signature(merged) != SyncMerge.Signature(remote.Items);
                if (pushNeeded)
                {
                    var push = await _backend.PushAsync(new SyncSnapshot(merged, remote?.Version), ct);
                    if (push.Outcome == SyncPushOutcome.Conflict)
                        continue;   // remote moved under us — re-pull, re-merge, retry
                }

                await ApplyMergedSuppressed(merged);   // wrapped so our own write doesn't re-trigger a debounce
                LastSyncedAt = DateTimeOffset.Now;
                return Finish(pushNeeded ? SyncResult.Ok : SyncResult.UpToDate);
            }

            LastError = "Cloud kept changing under us; gave up after retries.";
            return Finish(SyncResult.Failed);
        }
        catch (OperationCanceledException)
        {
            return Finish(SyncResult.Failed);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return Finish(SyncResult.Failed);
        }
        finally
        {
            IsSyncing = false;
            _syncGate.Release();
            RaiseStateChanged();
        }
    }

    private async Task ApplyMergedSuppressed(IReadOnlyList<SongListItem> merged)
    {
        _suppressLocalChanged = true;
        try
        {
            // Atomic merge-into (not overwrite): preserves any local edit that landed during the network pull.
            await _local.ApplyMergedAsync(merged);
        }
        finally
        {
            _suppressLocalChanged = false;
        }
    }

    private SyncResult Finish(SyncResult result)
    {
        LastResult = result;
        if (result is SyncResult.Ok or SyncResult.UpToDate)
            LastError = null;
        return result;
    }

    private void OnLocalChanged(object? sender, EventArgs e)
    {
        if (_suppressLocalChanged)
            return;
        if (_backend.AuthState != SyncAuthState.SignedIn)
            return;

        _debounceCts?.Cancel();
        var cts = _debounceCts = new CancellationTokenSource();
        _ = DebouncedSync(cts.Token);
    }

    private async Task DebouncedSync(CancellationToken ct)
    {
        try { await Task.Delay(DebounceMs, ct); }
        catch (OperationCanceledException) { return; }   // superseded by a newer edit
        await SyncNowAsync(ct);
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        RaiseStateChanged();
        if (_backend.AuthState == SyncAuthState.SignedIn)
            _ = SyncNowAsync();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _local.Changed -= OnLocalChanged;
        _backend.AuthStateChanged -= OnAuthStateChanged;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _syncGate.Dispose();
    }
}
