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
- **Android**: the Android SDK, a JDK 17+, and an emulator or a connected device. If you have neither SDK nor JDK, .NET Android can fetch both at the versions this project targets:
  ```bash
  dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android -t:InstallAndroidDependencies \
    -p:AndroidSdkDirectory=$HOME/Library/Android/sdk -p:JavaSdkDirectory=$HOME/Library/Android/jdk \
    -p:AcceptAndroidSDKLicenses=true
  ```
  Then export `ANDROID_HOME` and `JAVA_HOME` at those paths so plain `dotnet build` finds them — otherwise every build needs the `-p:AndroidSdkDirectory=… -p:JavaSdkDirectory=…` flags. (The warnings that target logs on its *first* run are from the evaluation pass before the SDK exists; they clear once it's installed.)
- **iOS**: a paired Mac (iOS cannot be built on Windows).
- **macOS (Mac Catalyst)**: full **Xcode** — Command Line Tools alone is not enough. Point the toolchain at it with `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`.

> Restore walks **every** target framework the project declares, even when you build a single head with `-f`, so a build fails until all of them have workloads. `dotnet workload restore KHost.Mobile/KHost.Mobile.csproj` installs exactly the set this project needs.

### Build & run

```bash
# Android
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Deploy and launch on a connected Android device / emulator
dotnet build KHost.Mobile/KHost.Mobile.csproj -t:Run -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Run on Windows — the quickest way to iterate on the Blazor UI (no emulator needed)
dotnet run --project KHost.Mobile -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"

# Run on macOS — the Mac equivalent, same story (no simulator needed)
dotnet run --project KHost.Mobile -f net10.0-maccatalyst "-p:BaseOutputPath=./obj/_build"
```

> `-p:BaseOutputPath=./obj/_build` keeps build output out of the IDE's `bin/` folder so it doesn't get locked while the IDE is open.

### Backing up on-device data (before a risky redeploy)

Deploying with `-t:Run` **updates the app in place** — the on-device data (`files/*.json`, `shared_prefs/`) survives. It's only wiped by an **uninstall**, and the sneaky way that happens is a *reinstall you didn't ask for*: deploying a build signed with a **different debug keystore** (fresh machine, regenerated `~/.android/debug.keystore`) fails to install over the existing app with a signature mismatch, and the tooling falls back to uninstall + reinstall — taking your singers, song lists, tonight sets, venues and settings with it. A manual "uninstall to fix a launch crash" does the same.

The host test suites (Unit + Integration) run against a throwaway temp folder and **never touch device data** — so the risk is device deploys, not tests. **Back up before any redeploy that might reinstall, or before any manual uninstall:**

```bash
dotnet run scripts/backup-device-data.cs -- backup                 # timestamped .tar.gz -> device-backups/ (gitignored)
dotnet run scripts/backup-device-data.cs -- list                   # what backups exist
dotnet run scripts/backup-device-data.cs -- inspect <file.tar.gz>  # peek inside one
dotnet run scripts/backup-device-data.cs -- restore <file.tar.gz>  # push a backup back onto the device
#   -s, --serial <serial>   target one of several attached devices (also honors $ANDROID_SERIAL)
#   restore also takes -y/--yes to skip the confirmation
```

The script is a **.NET 10 file-based app** (`scripts/*.cs` run with `dotnet run` — the repo convention, cross-platform with no dependency beyond the SDK). It pulls the app's private data via `adb run-as khost.mobile` (Debug builds only — no root) into `device-backups/`, which is gitignored so real singer data never reaches GitHub. `restore` force-stops the app and puts the data back; relaunch to see it.

> **Wireless adb tip:** the wireless-debugging **port rotates each session** — read it live from Settings → Developer options → Wireless debugging. If `adb connect` wedges after a failed attempt, `adb kill-server && adb start-server` clears it.

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

### Driving the running UI

Neither suite touches the UI; gesture- and sheet-shaped changes are verified by driving the app's WebView over CDP with the tools in **[`playwright/`](playwright/README.md)**. Split by target: **`playwright/device/`** (full Playwright) and **`playwright/emulator/`** (raw CDP with real-touch `tap`/`swipeDown` — the emulator's older WebView rejects Playwright's connect handshake). Each has a `walk_tutorial.mjs`; the README there has the attach flow, examples, and the on-device gotchas.

## 📸 Screenshots

**Screenshot / mobile-preview target size:** **786 × 1704 px** — a **393 × 852** (iPhone 15/16) viewport at **2× device-pixel-ratio**. Capture screenshots and size the mobile preview to this so everything lines up with the screenshot grid.

## 🎨 Design notes

**Album art — why `blob:` URLs and not `<img src>` / `file://`.** Covers are cached as plain image files in the app's private data directory (`Data/album-art/`, named by a hash of the source URL). But the Blazor WebView serves only the bundled, read-only `wwwroot`, and its page origin is `https://0.0.0.1` — so it has **no route to a file in the data directory**: `file://` access to the app-private dir is sandbox-blocked, an `https` page loading a `file://` resource is a cross-origin/mixed-content violation, and `wwwroot` can't be written to at runtime.

Referencing the cached file directly would therefore need a **per-platform serving handler** — WebView2 `SetVirtualHostNameToFolderMapping`, Android `WebViewAssetLoader` / `shouldInterceptRequest`, iOS `WKURLSchemeHandler` — three separate native implementations, the riskiest of which (Android) can't be verified without an on-device run.

Instead, the cover bytes are streamed to the WebView via a `DotNetStreamReference` and turned into a `blob:` object URL in `wwwroot/js/album-art.js` — **one implementation that behaves identically on every platform**, so it's verifiable once. The card's CSS background then holds a short `blob:` URL rather than a base64 `data:` copy of every image. `js/album-art.js` owns the object-URL lifecycle (revoked when a cover is replaced, on a singer switch, and on page teardown). The platform-serving approach remains a valid alternative if the C#↔JS transfer ever becomes a bottleneck.

**Asking for a cover is what fetches it.** `IAlbumArtService.UriFor(song)` returns the `blob:` URL if it's ready and otherwise *starts the work to get it* — discovering the artwork URL (iTunes, then Deezer when iTunes has no cover), downloading, caching, handing it to the WebView — then raises `Changed` so the surface repaints. It replaced a loader whose callers had to pre-declare a page of songs to load. That put the burden in the wrong place: every surface had to remember to do it, and any surface showing a song from *outside* the page it declared silently rendered a blank card. The 🎲 result sheet hit exactly that and needed a bespoke workaround, which this deletes. Callers now only render what they're given, so **a surface that displays cover art needs no art code at all** — but it does need to subscribe to `Changed`, because covers land after the render that asked for them and the art is a Blazor-rendered inline style.

Two things about it are counter-intuitive enough to be worth stating, both learned by measuring on-device:

- **There is deliberately no cache size cap.** An LRU cap was tried and thrashed. My Songs keeps every card it has scrolled past in the DOM and asks for all of their covers on every render, so any cap below the rendered-card count evicts a cover the next render immediately asks for again — an endless fetch/evict loop that never settled. Capping safely needs to know which cards are in the *viewport*, which the service can't see and the renderer doesn't track. Covers are therefore held until something explicitly drops them (a singer switch, an edit, clearing the cache), which is what the old design did in practice anyway.
- **Discovery is paced, downloads aren't.** Because scrolling now asks about every song it renders, and a typical library is ~80% coverless, an unpaced sweep fires hundreds of back-to-back iTunes calls and earns a rate-limit block. Each lookup that actually goes to the network is followed by a short pause; fetching an already-known cover URL hits the artwork CDN instead and runs at full speed.

**Crash-safe store writes.** Every JSON store writes to a same-directory `.tmp` file and atomically renames it over the target (`AtomicFile.WriteAsync`) — a same-volume rename, so a write interrupted by an app kill or power loss leaves the *last good* file intact instead of a truncated one. (The load path treats a corrupt file as "start empty", which for a direct overwrite would silently lose the whole list.) A file that fails to parse on load is moved aside to a `.corrupt` sibling rather than being overwritten by the next save, so the bad bytes are preserved for recovery.

**Split button — one control, a default action plus a menu.** `SplitButton` / `SplitButtonItem` (`Components/`) render a primary action segment beside a chevron that drops a menu of related actions — e.g. *Mark sung* with alternate ways to log it, or *Find on YouTube* with Spotify / KaraFun / Lyrics behind the chevron to reclaim sheet height. Reusable: pass the default via `OnPrimary` and the extras as `SplitButtonItem` children (each takes `Icon` / `Description` / `Separated`), with `Direction` (Down/Up, for buttons low in a sheet), `Align`, and `Variant` (Primary/Tonal/Secondary) knobs. Dismissal reuses the header ⋮ menu's approach — a transparent full-screen scrim for an outside tap, plus `IBackButtonService` so the Android back button closes the menu instead of navigating — so there's **no bespoke JS**; it's styled with the shared `.btn` variants and `--kh-` tokens.

**Surprise me — tap to roll, hold for options.** The 🎲 is a second FAB above the "+", deliberately 75% its size, offset up and to its right, and tonal rather than solid: three signals that it's the secondary action on the screen. A tap rolls immediately and shows the pick in `RollResultSheet` rather than opening the song outright — rerolling is the common case, and going through the detail sheet each time made it a three-tap loop. A press-and-hold opens the options. Since no assistive-tech gesture maps to a long press, the result sheet's *Options* action is the reachable equivalent — the same pattern Venues and Singers use for press-and-hold.

The result is rendered as the app's own `.song-card` (same classes, same album-art treatment, just taller) so a suggestion looks like it was lifted out of the list rather than announced by a system message. It started as a snackbar and that was wrong twice over: it borrowed the undo toast's deliberately-dark pill, which ignores the theme tokens, and it offered no dismissal that wasn't a navigation. Being a `Sheet` fixes both — the backdrop, ✕, pull-down-to-dismiss and scroll lock all come for free. *Add to tonight* is the primary button, not *Open*: lining a song up is what a singer usually wants from a suggestion, and reading its details is the follow-up.

One trap worth knowing: covers are cached **only for the rendered page**, but a roll can land on any song in the library, so the sheet needs `EnsureRollArtAsync` to fetch the pick's cover explicitly. It passes the current page along in the same `LoadAsync` call because each call cancels the previous run — asking for one cover on its own would drop the page's in-flight downloads and blank the list behind the sheet. (Only ~20% of a typical library has an artwork URL at all, so a plain card is the common, correct outcome; the hero height is scoped to `.song-card--art` so a coverless pick doesn't render a dead gap.)

The draw rules live in `Services/SurprisePicker.cs`, pure and deterministic given a roll value, so the narrowing and the star weighting are unit-tested without a UI or an RNG. Two things worth keeping: a restriction that would empty the pool is **skipped rather than honoured** (a one-tap picker has nowhere to explain "no matches"), and every song keeps a small weight floor so a run of bad nights can't make one undrawable. `press-hold.js` owns the pointer sequence rather than using `@onclick` plus a timer, because the WebView fires its own long-press callout early and still delivers a click afterwards — which would run the hold *and* then roll.

**Year picker — a grid of the library's own years.** `YearPickerSheet` (`Components/`) sets either end of the My Songs year filter. Three decisions worth keeping: it offers **only years actually present in the library**, not every year in the `min`–`max` span, so a pick can never produce an empty result set (the filter's `_libraryYears` comes off the same pass that computes the bounds); it lays them out as a **grid rather than a list**, because a span like 1959–2024 is 65 rows to scroll but only 13 as a five-column grid — the same reason the platform date pickers show years in a grid; and years that would put the two handles the wrong way round render **disabled rather than being silently clamped**, so the year you tap is the year you get. The dual-handle slider is unchanged and still drives the same two values.

**Mac Catalyst head — a layout preview, not a product.** There is no desktop KHost Cue and none is planned. The Catalyst head exists for the same reason as the Windows head: iterating on the Blazor UI without waiting on an emulator. Everything about it is tuned for that and nothing for shipping — it builds `maccatalyst-arm64` only (native on Apple silicon, one slice instead of two), and `App.CreateWindow` opens it at exactly the **393 × 852** mobile-preview viewport documented under Screenshots, so what you see matches the screenshot grid. It stays resizable so you can drag it wider to find where a layout breaks.

Landing that exact viewport takes several Catalyst workarounds (a min = max size-restriction pin, a measured title-bar correction, and the Mac idiom in `UIDeviceFamily` for 1 : 1 point scaling) — the mechanics and their reasons are commented where they live, in `App.PinToMobilePreviewViewport` and the Catalyst `Info.plist`. To capture a screenshot from this head, grab the window (`screencapture -l <windowid>`) and crop the title bar off the top — the remainder is exactly 786 × 1704.

Treat a wide Catalyst window as a diagnostic, not a bug: the shell is deliberately mobile-first (fixed bottom tab bar, full-bleed cards, swipe and press-and-hold gestures), so stretching it *should* look wrong. Don't add desktop breakpoints to `wwwroot/app.css` to "fix" it.

Mouse input covers the gestures: `swipe.js` runs off pointer events, so click-and-drag is a swipe and click-and-hold is a press-and-hold. Where a gesture is awkward to trigger, the reachable equivalents added for assistive tech work too — Venues' *Active* toggle and the singer sheet's *Switch to this singer*.

## 📁 Project structure

| Project | Role |
|---|---|
| `KHost.Mobile` | The MAUI Blazor Hybrid app — a thin native shell hosting the Razor UI in `Components/`. |
| `KHost.Mobile.Clients` | Client library: playlist import (Spotify / YouTube Music), iTunes metadata lookup, Deezer cover-art fallback, and LRCLIB lyrics lookup. |
| `KHost.Mobile.UnitTests` | xUnit unit tests for the pure, no-I/O logic (parsers, `Genres`, `SongListItem`). |
| `KHost.Mobile.IntegrationTests` | xUnit integration tests for the JSON stores against a real temp folder, via a fake `IAppDataDirectory`. |
