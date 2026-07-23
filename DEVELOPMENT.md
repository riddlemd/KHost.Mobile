# 🧑‍💻 Development & design notes

Developer-facing docs for **KHost Cue** — how to build and test it, and the reasoning behind a few non-obvious parts. For the product overview, features, and screenshots, see **[README.md](README.md)**. For AI-agent / contributor coding conventions (style, patterns, gotchas), see **[AGENTS.md](AGENTS.md)**.

## 🛠️ Tech stack

- **[.NET 10](https://dotnet.microsoft.com/)** with **[.NET MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui/)** — native iOS/Android shell hosting a Razor (Blazor) UI.
- On-device storage in JSON files behind interfaces (`ISongListStore`, `ITonightStore`, `IVenueStore`, `ISingerStore`, `ILyricsCache`) that keep storage concerns out of the UI. The song-list and tonight stores are namespaced per singer, so each person's lists live in their own file.

## 🚀 Building from source

### Prerequisites

- **.NET 10 SDK** with the MAUI workload:
  ```bash
  dotnet workload install maui
  ```
- **Android**: the Android SDK and an emulator or a connected device.
- **iOS**: a paired Mac (iOS cannot be built on Windows).

### Build & run

```bash
# Android
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Deploy and launch on a connected Android device / emulator
dotnet build KHost.Mobile/KHost.Mobile.csproj -t:Run -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Run on Windows — the quickest way to iterate on the Blazor UI (no emulator needed)
dotnet run --project KHost.Mobile -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"
```

> `-p:BaseOutputPath=./obj/_build` keeps build output out of the IDE's `bin/` folder so it doesn't get locked while the IDE is open.

### Sample data for testing

Need songs to populate the list while testing? This public **YouTube Music** playlist imports cleanly via **Import & Export → YouTube Music**:

```text
https://music.youtube.com/playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM
```

## 🧪 Testing

Two xUnit projects, split by what they touch. Both must pass before any commit:

```bash
# Unit tests — pure, no-I/O logic (playlist/metadata/lyrics parsers, Genres.Map, SongListItem computed properties)
dotnet test KHost.Mobile.UnitTests/KHost.Mobile.UnitTests.csproj "-p:BaseOutputPath=./obj/_build"

# Integration tests — the JSON stores against a real temp folder (real file I/O + serialization)
dotnet test KHost.Mobile.IntegrationTests/KHost.Mobile.IntegrationTests.csproj "-p:BaseOutputPath=./obj/_build"
```

Neither test project needs the MAUI workload: they target plain `net10.0`. The MAUI-free source they cover (models, stores) is pulled in via linked `<Compile>` since a `net10.0` project can't reference the MAUI head. The stores' only device dependency — the app-data folder — is abstracted behind `IAppDataDirectory`, which the integration tests point at a throwaway temp directory.

## 📸 Screenshots

**Screenshot / mobile-preview target size:** **786 × 1704 px** — a **393 × 852** (iPhone 15/16) viewport at **2× device-pixel-ratio**. Capture screenshots and size the mobile preview to this so everything lines up with the screenshot grid.

## 🎨 Design notes

**Album art — why `blob:` URLs and not `<img src>` / `file://`.** Covers are cached as plain image files in the app's private data directory (`Data/album-art/`, named by a hash of the source URL). But the Blazor WebView serves only the bundled, read-only `wwwroot`, and its page origin is `https://0.0.0.1` — so it has **no route to a file in the data directory**: `file://` access to the app-private dir is sandbox-blocked, an `https` page loading a `file://` resource is a cross-origin/mixed-content violation, and `wwwroot` can't be written to at runtime.

Referencing the cached file directly would therefore need a **per-platform serving handler** — WebView2 `SetVirtualHostNameToFolderMapping`, Android `WebViewAssetLoader` / `shouldInterceptRequest`, iOS `WKURLSchemeHandler` — three separate native implementations, the riskiest of which (Android) can't be verified without an on-device run.

Instead, the cover bytes are streamed to the WebView via a `DotNetStreamReference` and turned into a `blob:` object URL in `wwwroot/js/album-art.js` — **one implementation that behaves identically on every platform**, so it's verifiable once. The card's CSS background then holds a short `blob:` URL rather than a base64 `data:` copy of every image, and art is loaded only for the currently-paged cards, so image memory stays bounded by what's on screen. `js/album-art.js` owns the object-URL lifecycle (revoked when a cover is replaced, on a singer switch, and on page teardown). The platform-serving approach remains a valid alternative if the C#↔JS transfer ever becomes a bottleneck.

**Crash-safe store writes.** Every JSON store writes to a same-directory `.tmp` file and atomically renames it over the target (`AtomicFile.WriteAsync`) — a same-volume rename, so a write interrupted by an app kill or power loss leaves the *last good* file intact instead of a truncated one. (The load path treats a corrupt file as "start empty", which for a direct overwrite would silently lose the whole list.) A file that fails to parse on load is moved aside to a `.corrupt` sibling rather than being overwritten by the next save, so the bad bytes are preserved for recovery.

**Split button — one control, a default action plus a menu.** `SplitButton` / `SplitButtonItem` (`Components/`) render a primary action segment beside a chevron that drops a menu of related actions — e.g. *Mark sung* with alternate ways to log it, or *Find on YouTube* with Spotify / KaraFun / Lyrics behind the chevron to reclaim sheet height. Reusable: pass the default via `OnPrimary` and the extras as `SplitButtonItem` children (each takes `Icon` / `Description` / `Separated`), with `Direction` (Down/Up, for buttons low in a sheet), `Align`, and `Variant` (Primary/Tonal/Secondary) knobs. Dismissal reuses the header ⋮ menu's approach — a transparent full-screen scrim for an outside tap, plus `IBackButtonService` so the Android back button closes the menu instead of navigating — so there's **no bespoke JS**; it's styled with the shared `.btn` variants and `--kh-` tokens.

## 📁 Project structure

| Project | Role |
|---|---|
| `KHost.Mobile` | The MAUI Blazor Hybrid app — a thin native shell hosting the Razor UI in `Components/`. |
| `KHost.Mobile.Clients` | Client library: playlist import (Spotify / YouTube Music), iTunes metadata lookup, Deezer cover-art fallback, and LRCLIB lyrics lookup. |
| `KHost.Mobile.UnitTests` | xUnit unit tests for the pure, no-I/O logic (parsers, `Genres`, `SongListItem`). |
| `KHost.Mobile.IntegrationTests` | xUnit integration tests for the JSON stores against a real temp folder, via a fake `IAppDataDirectory`. |
