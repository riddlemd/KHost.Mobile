# Android (Google Drive) sync — setup & smoke-test checklist

The cloud-sync code for Android is **implemented but not runtime-verified**: the local shared core (soft-delete,
tombstones, the LWW merge engine, the coordinator) is fully verified on the Windows head against a file-backed fake
cloud, but the actual Google OAuth + Drive round-trip cannot be exercised without a real OAuth client id and a Google
login on a device/emulator. This doc is what turns it on and proves it works.

## Architecture (what's already built)

```
UI ── ISongListStore ─┐
                      ├─ JsonFileSongListStore  (local source of truth; soft-delete tombstones, UpdatedAt stamps)
SyncCoordinator ──────┤     (also ILocalSongStore: GetRawAsync / ReplaceAllAsync for the whole snapshot incl tombstones)
   │  pull → SyncMerge → apply-local → push
   └─ ISyncBackend ──── GoogleDriveBackend (Android)  ── GoogleOAuthClient (WebAuthenticator + PKCE)
                        FakeSyncBackend    (Windows dev head — file-backed "cloud")
                        NullSyncBackend    (iOS for now, and any unconfigured build → app runs local-only)
```

- **Merge model:** last-write-wins per item, keyed on `SongListItem.Id`, decided by `UpdatedAt`, with `DeletedAt`
  tombstones so deletions propagate. Item-granular, not field-granular (`SyncMerge.cs`).
- **Transport:** the entire list is one JSON blob in the Drive **appDataFolder** (hidden per-app folder,
  `drive.appdata` scope — invisible in the user's Drive, never touches their other files).
- **Optimistic concurrency:** the Drive file's `headRevisionId` is the snapshot `Version`. A push made against a stale
  revision is rejected as `Conflict`; the coordinator re-pulls, re-merges, and retries (up to 3×).
- **Per-ecosystem only:** Android↔Google Drive, iOS↔iCloud. No cross-ecosystem sync by design.

## One-time Google Cloud setup

1. **Create/choose a Google Cloud project** — <https://console.cloud.google.com>.
2. **Enable the Google Drive API** — APIs & Services → Library → "Google Drive API" → Enable.
3. **OAuth consent screen** — External; add your Google account as a **Test user** (so you don't need app verification
   for personal use). Add the scope `https://www.googleapis.com/auth/drive.appdata` (plus `openid`, `email` for the
   account label). Note: `drive.appdata` is a **non-sensitive** scope for the app's own hidden folder, so verification
   is generally not required for personal/test use.
4. **Create an OAuth client id.** This is the fragile bit for a WebAuthenticator custom-scheme + PKCE flow:
   - The code expects a **reversed-client-id custom scheme** redirect (`com.googleusercontent.apps.<id>:/oauth2redirect`),
     which is the installed-app convention Google issues for the **iOS** client type. Create an **iOS** OAuth client
     (bundle id can be your Android package `khost.mobile`) to obtain a client id whose reversed form is a usable
     custom scheme. (Google's dedicated **Android** client type is for the native Google Sign-In SDK and does *not*
     hand you a custom-scheme redirect, which is why we don't use it here.)
   - If Google rejects the custom-scheme flow for your account, the fallback is native Google Sign-In (Play Services)
     — a larger change; the `GoogleOAuthClient` seam is where it would swap in.

## Wire the client id into the app

1. **`KHost.Mobile/Services/SyncConfig.cs`** — set `GoogleClientId` to your real client id. It's a *public* value
   (PKCE, not secrecy, protects the flow), but **don't commit a real one** — set it locally, or assign
   `SyncConfig.GoogleClientId` early in `MauiProgram` from an untracked config/user-secret.
2. **`KHost.Mobile/Platforms/Android/Sync/GoogleAuthCallbackActivity.cs`** — replace the `DataScheme` placeholder
   `com.googleusercontent.apps.REPLACE_WITH_REVERSED_CLIENT_ID` with your reversed client id. It must equal what
   `SyncConfig.GoogleRedirectScheme` computes (client id `1234-abcd.apps.googleusercontent.com` → scheme
   `com.googleusercontent.apps.1234-abcd`). This is a compile-time attribute, so it can't read `SyncConfig` — keep the
   two in step by hand.
3. Rebuild the Android head:
   `dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"`

## Smoke test (needs a device/emulator + a Google account)

Deploy with `dotnet build -t:Run -f net10.0-android` (see CLAUDE.md — `adb install` crashes with Fast Deployment).

1. ☐ Open the header menu (⋮). A **"Google Drive"** section shows with **"Sign in to Google Drive"** (AuthState
   `SignedOut`). If it's missing, `SyncConfig.IsGoogleConfigured` is false — the client id is still the placeholder.
2. ☐ Tap **Sign in** → the system browser opens Google consent → approve → you're returned to the app and the status
   flips to **"Synced · your@email"**. (If the browser dead-ends, the `DataScheme` doesn't match the client id.)
3. ☐ Add a song → within ~2s the status shows a fresh sync. Confirm the blob exists: Drive API "Manage third-party
   apps" won't show appDataFolder contents, so use the **OAuth Playground** or an API call
   `GET https://www.googleapis.com/drive/v3/files?spaces=appDataFolder` to see `khost-cue-songs.json`.
4. ☐ **Second device / reinstall:** sign in with the same account → the song list pulls down.
5. ☐ **Delete propagation:** remove a song on device A → on device B (or after a reinstall) it's gone (tombstone
   applied), not resurrected.
6. ☐ **Concurrent edit:** edit the same song on both devices while offline, then let both sync → the newer `UpdatedAt`
   wins; no crash, no duplicate.
7. ☐ **Sign out** → status returns to "Sign in to Google Drive"; local list is untouched.

## Known limitations / follow-ups

- The optimistic-concurrency check (`headRevisionId` compare-then-write) has a small TOCTOU window — fine for a single
  user's own devices, not for true multi-writer contention.
- **Item-granular LWW loses concurrent list-field edits.** Merge picks the whole newer item per `Id`; it does not
  field-merge. So if the *same* song is marked "sang" (a new `SungDates` entry) on two devices while both are offline,
  the merge keeps only one device's copy — one performance timestamp is lost. Deliberate simplification for a single
  user's own devices; revisit with a set-union on `SungDates` if this bites. (Concurrent edits to *different* songs are
  unaffected — that path is verified.)
- Custom-scheme OAuth is increasingly restricted by Google; if it stops working, move `GoogleOAuthClient` to native
  Google Sign-In.
- iOS/iCloud (`AppleCloudBackend`) is design-only until a Mac is available; it slots in behind the same `ISyncBackend`
  with zero coordinator/merge changes.
