# KHost Cue

**You're on cue.** — the singer & patron companion app for [KHost](../KHost), open-source karaoke host software.

KHost Cue is a **.NET MAUI Blazor Hybrid** app for **iOS and Android** (built on **.NET 10**). Keep a personal wishlist of songs you'd like to sing, track how each one went, and revisit your sung history — all on your device. It's designed to eventually connect to the **KHost.Online** cloud relay so singers can scan a venue QR / enter a join code, search the live library, request a song, and watch the queue — but today it ships as an **offline, local-only** experience.

> The app never talks to the KHost desktop app directly. Its online features (deferred) go through the KHost.Online relay.

## Features

**My Songs** — an on-device wishlist of songs to sing.
- Add songs (free-text title/artist) with optional genre and release year.
- A card-based list with a sortable header (song / artist / rating), text + genre + year + rating filters, and swipe-to-remove with undo.
- Tap a card for a detail sheet: mark sung (a running history of dates), rating, notes, and quick **Find on YouTube** / **Find on Spotify** buttons.
- Favorites float to the top; optionally scroll to a song when you favorite it.

**Import / Export**
- Import from a public **Spotify** or **YouTube Music** playlist link (token-free, title + artist), or from a KHost Cue `.json` export.
- Post-import review that auto-fills year & genre.
- Export your whole list to a `.json` file.

**Auto-fill** — keyless year & genre lookup from the iTunes Search API when you open a song (per-song, rate-limit friendly).

**Settings** — feature toggles (all persisted): iTunes auto-fill, the Find-on-YouTube / Find-on-Spotify buttons, and scroll-to-song-on-favorite.

Plus: mobile-first shell with a header menu, light/dark theme (brand violet `#7c3aed`), and an About page.

## Tech stack

- **.NET 10** · **.NET MAUI Blazor Hybrid** (Razor UI in a native shell)
- Targets: `android`, `ios`, `windows` (the Windows head is used only for fast desktop UI iteration; iOS/Android are the shipping targets)
- Local persistence: a single JSON file under `FileSystem.AppDataDirectory`, behind an `ISongListStore` interface so a cloud-sync implementation can drop in later without UI changes.

## Repository layout

This repo is one of three kept as **siblings under `repos/`** — the reference to `KHost.Contracts` is a relative project reference, so the folders must stay side by side:

```
repos/
├── KHost/            PUBLIC  — the desktop karaoke host (Blazor Server + Avalonia)
├── KHost.Online/     PRIVATE — the cloud relay (ASP.NET Core: REST + SignalR) + KHost.Contracts (wire DTOs)
└── KHost.Mobile/     THIS repo — the MAUI Blazor Hybrid app
```

### Projects (`KHost.Mobile.slnx`)

| Project | Role |
|---|---|
| `KHost.Mobile` | MAUI Blazor Hybrid host. Thin shell; UI is Razor components under `Components/`. |
| `KHost.Mobile.Client` | Typed HTTP + SignalR client, playlist import (Spotify / YouTube Music), and iTunes metadata lookup. |
| `KHost.Contracts` | *(referenced; lives in the KHost.Online repo)* wire DTOs + the `IQueueClient` hub interface. |

## Getting started

### Prerequisites
- **.NET 10 SDK** with the **MAUI workload** (`dotnet workload install maui`).
- The **`KHost.Online`** repo checked out as a **sibling folder** (needed for the relative `KHost.Contracts` reference).
- For Android: the Android SDK + a device or emulator. For iOS: a paired Mac (iOS cannot build on Windows).

### Build & run

```bash
# Android head — the primary "green signal" build on Windows.
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Deploy + launch on a connected Android device / emulator.
dotnet build KHost.Mobile/KHost.Mobile.csproj -t:Run -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Windows head — fastest way to iterate the Blazor Hybrid UI (no emulator).
dotnet run --project KHost.Mobile -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"

# Client library on its own.
dotnet build KHost.Mobile.Client/KHost.Mobile.Client.csproj
```

`-p:BaseOutputPath=./obj/_build` mirrors the KHost repo convention (redirects output so it doesn't lock the IDE's `bin/`).

### Gotchas
- **iOS cannot build on Windows** without a paired Mac — a bare solution build surfaces Apple-toolchain errors that aren't your code. Verify with the **Android head**; iterate UI with the **Windows head**.
- **"Won't restore"** almost always means the `KHost.Online` sibling folder is missing or moved.
- `KHost.Contracts` must stay a plain `net10.0` library with **zero package references** — a platform MAUI head can consume a base `net10.0` library, but not vice-versa.

## Roadmap

Server integration is scaffolded but deferred. The eventual online slice — **join venue → search library → request song → live queue** — will run against KHost.Online over typed HTTP + a SignalR `HubConnection`, reusing the `KHost.Contracts` DTOs already in place. Because all local data sits behind `ISongListStore`, sync can land without a UI rewrite.

## License

KHost.Mobile is licensed under the [PolyForm Shield License 1.0.0](LICENSE) — the same license as [KHost](../KHost).

You may use, modify, and self-host KHost.Mobile for any purpose, **including commercial use** (for example, running it for your own karaoke events). You may **not** use it to provide a product or service that competes with KHost or KHost.Mobile — including offering it or a derivative as a hosted/managed service (SaaS), or redistributing it under a different brand — without a separate license.

**Commercial, SaaS, and OEM licenses are available** for those uses — contact Michael Riddle <riddlemd@gmail.com>.
