using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

// ============================================================================================
//  Cloud-sync backend — SKETCH (design scaffold, no implementations yet)
// --------------------------------------------------------------------------------------------
//  Per-ecosystem sync only: iOS syncs to the user's iCloud, Android to the user's Google Drive.
//  The two clouds never talk, and that is by design — cross-ecosystem (iPhone <-> Android) sync
//  is explicitly out of scope. Each platform ships an independent ISyncBackend; the export/import
//  share-sheet flow (separate) is the manual bridge for the rare cross-ecosystem migration.
//
//  Layering:
//    UI ── ISongListStore ──> SyncingSongListStore (phase 3, the reconcile engine)
//                                   │  local source of truth stays JsonFileSongListStore
//                                   └─> ISyncBackend  (this file — the transport)
//                                          ├─ AppleCloudBackend   (Platforms/iOS,  CloudKit / iCloud KVS)
//                                          └─ GoogleDriveBackend  (Platforms/Android, Drive appDataFolder)
//
//  Merge model: last-write-wins per item, keyed on SongListItem.Id, decided by UpdatedAt, with
//  DeletedAt tombstones so deletions propagate. The backend is a dumb transport — it does NOT merge;
//  SyncingSongListStore pulls, merges locally, then pushes. PushAsync only guards against a remote
//  that moved underneath us (optimistic concurrency via the snapshot Version token).
// ============================================================================================

/// <summary>Auth/availability state of a platform sync backend, for driving the header-menu UI.</summary>
public enum SyncAuthState
{
    /// <summary>The backend can't run on this build/device (no iCloud entitlement, no OAuth client id, etc.).</summary>
    NotConfigured,

    /// <summary>Configured but the user hasn't connected an account (Android before Google Sign-In).</summary>
    SignedOut,

    /// <summary>Connected and ready to sync. On iOS this is the ambient state whenever iCloud is enabled.</summary>
    SignedIn,
}

/// <summary>Outcome of a <see cref="ISyncBackend.PushAsync"/>.</summary>
public enum SyncPushOutcome
{
    /// <summary>The push was written; <see cref="SyncPushResult.Snapshot"/> carries the new remote Version.</summary>
    Applied,

    /// <summary>Rejected: the remote changed since the Version we pushed against. Caller should re-pull,
    /// re-merge, and retry. <see cref="SyncPushResult.Snapshot"/> is null.</summary>
    Conflict,
}

/// <summary>A full remote state: every item (including <see cref="SongListItem.DeletedAt"/> tombstones)
/// plus an opaque <paramref name="Version"/> token used for optimistic concurrency. <c>Version</c> is null
/// when nothing has been stored remotely yet (first-ever sync).</summary>
public sealed record SyncSnapshot(IReadOnlyList<SongListItem> Items, string? Version);

/// <summary>Result of a push: whether it applied and, if so, the stored snapshot with its bumped Version.</summary>
public sealed record SyncPushResult(SyncPushOutcome Outcome, SyncSnapshot? Snapshot);

/// <summary>
/// A platform's personal-cloud transport (iCloud on iOS, Google Drive on Android). Deliberately thin:
/// authenticate, pull the whole snapshot, push the whole snapshot. All merge/reconcile logic lives above
/// this, in <c>SyncingSongListStore</c>, so both platform backends share one tested merge implementation.
/// </summary>
public interface ISyncBackend
{
    /// <summary>Display name for UI/logging, e.g. "iCloud" or "Google Drive".</summary>
    string Name { get; }

    /// <summary>Current auth/availability, without triggering any UI. Drives the "Sign in / Sync now" affordances.</summary>
    SyncAuthState AuthState { get; }

    /// <summary>Account label to show the user (e.g. the signed-in Google email). Null on iOS (ambient iCloud)
    /// or when signed out.</summary>
    string? AccountLabel { get; }

    /// <summary>Raised when <see cref="AuthState"/> changes (sign-in completes, account removed) so the UI refreshes.</summary>
    event EventHandler? AuthStateChanged;

    /// <summary>Bring the backend to <see cref="SyncAuthState.SignedIn"/> if possible. On Android this launches the
    /// interactive Google Sign-In consent for the <c>drive.appdata</c> scope; on iOS it verifies the device's iCloud
    /// account and the app's iCloud container are usable. Returns the resulting state.</summary>
    Task<SyncAuthState> ConnectAsync(CancellationToken ct = default);

    /// <summary>Disconnect the cloud account (Android Google Sign-Out). No-op where the OS owns the identity (iOS iCloud).</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Fetch the entire remote snapshot, or null if nothing has been stored yet. Requires
    /// <see cref="SyncAuthState.SignedIn"/>.</summary>
    Task<SyncSnapshot?> PullAsync(CancellationToken ct = default);

    /// <summary>Write <paramref name="merged"/> back to the cloud. <paramref name="merged"/>.Version must be the
    /// token from the Pull this push was merged against; if the remote has advanced past it the write is rejected
    /// with <see cref="SyncPushOutcome.Conflict"/> (caller re-pulls, re-merges, retries). Pass a snapshot whose
    /// Version is null for the first-ever push.</summary>
    Task<SyncPushResult> PushAsync(SyncSnapshot merged, CancellationToken ct = default);
}
