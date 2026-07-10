# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**KHost.Mobile** — the singer/patron-facing companion app for [KHost](../KHost) (open-source karaoke host software). Built as a **.NET MAUI Blazor Hybrid** app (iOS + Android) on **.NET 10**. It does **not** talk to the KHost desktop app directly — it talks to the **KHost.Online** cloud relay (a separate private repo). Singers scan a venue QR / enter a join code, then search the library, request a song, and watch the live queue.

## Cross-repo topology

Three repos, kept as **siblings under `repos/`**:

```
repos/
├── KHost/            PUBLIC — the desktop karaoke host (Blazor Server + Avalonia). Untouched by mobile.
├── KHost.Online/     PRIVATE — the cloud relay (ASP.NET Core: REST + SignalR) + KHost.Contracts (the wire DTOs).
└── KHost.Mobile/     THIS repo — the MAUI Blazor Hybrid app.
```

- **The mobile app currently references none of the other repos — it builds standalone.** The online slice is deferred (see Roadmap), so the `KHost.Contracts` reference has been removed for now.
- When that slice lands, the shared code is `KHost.Contracts` (DTOs + the `IQueueClient` hub interface), which lives in the `KHost.Online` repo (it *is* the server's public API surface). Consume it as a published **NuGet package** — also how the public `KHost` client will — or a relative project reference during build-out. It must stay a plain `net10.0` library with **zero package references**: a platform MAUI head can consume a base `net10.0` library, but not vice-versa.
- **Never** reference `KHost.Abstractions`/`Domain`/EF from mobile. The wire contract is a projection, not the host's domain model.

## Solution / project layout

`KHost.Mobile.slnx` (mobile stays in its OWN solution so MAUI workloads never slow the desktop or server builds):

| Project | Role |
|---|---|
| `KHost.Mobile` | MAUI Blazor Hybrid host. Thin shell; UI is Razor components (`Components/`). |
| `KHost.Mobile.Client` | Client library: playlist import (Spotify / YouTube Music) + iTunes metadata lookup. The typed HTTP/SignalR server client is deferred with the online slice. |

> Razor UI lives in `KHost.Mobile/Components/` for now. If a PWA build is ever wanted, extract components into a Razor Class Library (`KHost.Mobile.UI`) — the Hybrid design keeps that door open with no rewrite.

## Commands

```bash
# Android head — THE green signal on Windows (iOS cannot build here; see gotcha).
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Windows head — fastest way to iterate the Blazor Hybrid UI (no emulator).
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"
dotnet run   --project KHost.Mobile -f net10.0-windows10.0.19041.0   # launch the UI on the desktop

# Client library on its own
dotnet build KHost.Mobile.Client/KHost.Mobile.Client.csproj
```

`-p:BaseOutputPath=./obj/_build` mirrors the KHost repo convention (redirects output so it doesn't lock VS's `bin/`).

## Gotchas

- **iOS cannot build on Windows** without a paired Mac. A bare `dotnet build` on the solution will surface iOS/Apple-toolchain errors that are **not** your code. Build the **Android head explicitly** to verify, and use the **Windows head** for fast UI iteration. iOS is validated when a Mac is in the loop.
- **`TargetFrameworks` is trimmed to `android;ios;windows`** (maccatalyst/tizen dropped) since the stated targets are iOS/Android. Don't re-add heads without a reason.
- This repo **builds standalone** — it no longer references the sibling `KHost.Online`/`KHost.Contracts` projects. (They return with the online slice; see Roadmap.)

## KHost.Online — the server it talks to

Server repo: `../KHost.Online` (see its own `CLAUDE.md`). First-slice REST + SignalR surface, all route strings in `KHost.Contracts/Routes.cs`:

| Method | Route (`Routes.Api.*`) | Purpose |
|---|---|---|
| POST | `/api/venues/join` | join code + display name → session token (`Bearer`) |
| GET | `/api/songs/search?q=` | filtered library (auth required) |
| POST | `/api/queue/request` | add self to queue for a song; broadcasts `QueueUpdated` |
| GET | `/api/queue` | current queue snapshot |
| Hub | `/hubs/queue` | SignalR push — connect with `?access_token=<sessionToken>` |

Auth (first slice): an opaque server-side session token sent as `Authorization: Bearer <token>` — a deliberate stand-in for a signed JWT later. Demo venue join code is `DEMO`.

`IQueueClient` push methods: `QueueUpdated(queue)`, `NowPlayingChanged(nowPlaying?)`, `YoureUpNext()`.

## Local features (current focus — no server yet)

The app currently ships **offline/local UI only**; it does not talk to KHost.Online yet. All local data sits behind an interface with a device-backed implementation, so a server-sync implementation can drop in later without UI changes.

**Mobile shell** (`Components/Layout/`): mobile-first — sticky top app bar, scrolling content, fixed bottom tab bar (`NavMenu`). "My List" is live; "Browse" (online library) and "History" (sang songs) are disabled roadmap placeholders. Theme in `wwwroot/app.css`: design tokens + light/dark via `prefers-color-scheme`; brand accent violet `#7c3aed`.

**"My Songs" list** — a patron's on-device wishlist of songs to sing (future: folds into a "sang songs" history).
- `Models/SongListItem.cs` — local-first: free-text title/artist, `SongListItemStatus` (`WantToSing` → extends to `Sang`), `AddedAt`, nullable `SungAt`/`LibrarySongId` for the future history + online-library link.
- `Services/ISongListStore.cs` — the UI binds to this ONLY. `Changed` event drives refresh.
- `Services/JsonFileSongListStore.cs` — persists a JSON list under `FileSystem.AppDataDirectory`; `SemaphoreSlim`-guarded, in-memory cache, swallows a corrupt file rather than crashing. Registered singleton in `MauiProgram`.
- `Components/Pages/MySongs.razor` — route `/`; add form (title required) + list + remove + empty state.

**Verified end-to-end** (Windows head, screenshots): shell/theme/dark-mode render; add → the app writes `Data/song-list.json`; kill + relaunch → the song is still there and renders (persistence read+write+restart all confirmed).

## Roadmap (server integration — deferred)

Eventual slice: **join venue → search library → request song → live queue** against KHost.Online. When it lands, `KHost.Mobile.Client` gains the typed HTTP + `HubConnection` server client and re-takes a `KHost.Contracts` reference (removed for now so this repo builds standalone). `KHost.Online`'s REST slice is scaffolded and runtime-verified in its own repo.

## Gotchas

- **`FileSystem.AppDataDirectory` is the `Data` SUBFOLDER**, i.e. `%LOCALAPPDATA%\KHost\khost.mobile\Data\` on unpackaged Windows (the parent folders are the appxmanifest `PublisherDisplayName` = `KHost` and the `ApplicationId` = `khost.mobile`) — NOT the parent `khost.mobile\`. Seeding/inspecting persisted state must target `Data\`. NOTE: builds from *before* the publisher/id rename wrote to the legacy `%LOCALAPPDATA%\User Name\com.companyname.khost.mobile\Data\`; that stale copy is ignored by current builds.
- The template's `Components/Routes.razor` has `FocusOnNavigate Selector="h1"`; pages here have no `<h1>`, so nothing auto-focuses. Harmless, but don't rely on autofocus.
- **UI test automation on the Windows head is flaky** — WebView2 swallows the *first* SendKeys burst after launch, and page scroll position varies between launches (fixed click coords drift). For persistence checks, prefer seeding/reading `Data\song-list.json` directly over driving the form.
- Do NOT commit or push unless explicitly asked. Secrets via user-secrets/config — never hard-coded or committed.

## Conventions

- File-scoped namespaces; `sealed record` for DTOs, mutable `class` for persisted editable entities (mirrors KHost).
