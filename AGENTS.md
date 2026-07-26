# AGENTS.md

Guidance for AI coding agents (Claude Code, GitHub Copilot, Cursor, etc.) working in this repository. `CLAUDE.md` imports this file, so this is the single source of truth.

**KHost.Mobile** (app name **"KHost Cue"**) — the singer/patron-facing companion app for [KHost](../KHost) (open-source karaoke host software). A **.NET MAUI Blazor Hybrid** app (iOS + Android) on **.NET 10**.

**Today the app is local/offline only** — a personal, on-device karaoke wishlist and "tonight" set list. It does **not** talk to any server yet. A future online slice (see Roadmap) will connect it to the **KHost.Online** cloud relay for join-a-venue → search → request → live queue; that work is deferred, so treat any server/queue references below as roadmap, not current behavior.

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
| `KHost.Mobile` | MAUI Blazor Hybrid host. Thin shell; UI is Razor components (`Components/`), local stores under `Services/`, models under `Models/`. |
| `KHost.Mobile.Clients` | Standalone client library — the outward-facing lookups: playlist import (`Spotify/`, `YouTubeMusic/`), iTunes metadata (`Enrichment/`), Deezer cover-art fallback (`Deezer/`), LRCLIB lyrics (`Lyrics/`), and the GitHub-Releases update check (`Updates/`). No MAUI dependency. |

> Razor UI lives in `KHost.Mobile/Components/` for now. If a PWA build is ever wanted, extract components into a Razor Class Library (`KHost.Mobile.UI`) — the Hybrid design keeps that door open with no rewrite.

## Commands

```bash
# Android head — THE green signal on Windows (iOS cannot build here; see gotcha).
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Windows head — fastest way to iterate the Blazor Hybrid UI on Windows (no emulator).
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"
dotnet run   --project KHost.Mobile -f net10.0-windows10.0.19041.0   # launch the UI on the desktop

# Mac Catalyst head — the macOS equivalent: fastest UI iteration on a Mac, no simulator.
# DEV-ONLY (layout preview). There is no desktop product; don't treat it as a shipping target.
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-maccatalyst "-p:BaseOutputPath=./obj/_build"
dotnet run   --project KHost.Mobile -f net10.0-maccatalyst            # launch the UI on the desktop

# Client library on its own
dotnet build KHost.Mobile.Clients/KHost.Mobile.Clients.csproj
```

`-p:BaseOutputPath=./obj/_build` mirrors the KHost repo convention (redirects output so it doesn't lock VS's `bin/`).

### Deploying to a device / emulator

```bash
# Build, deploy, AND launch on the connected Android device (physical Pixel or emulator).
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android -t:Run "-p:BaseOutputPath=./obj/_build"
# Target a specific device when more than one is attached:
#   "-p:AdbTarget=-s <serial>"      (e.g. -s emulator-5554, or the wireless-adb serial)
```

- **Always deploy the Debug build with `-t:Run`, never `adb install` the APK.** The Debug config keeps the .NET assemblies *outside* the APK (Fast Deployment) and relies on the MSBuild deploy target to push them to the device's `files/.__override__/`; a bare `adb install` launches then **crashes** with *"No assemblies found in '.../.__override__/...'. Assuming this is part of Fast Deployment. Exiting."* `-t:Run` does the push and starts the activity. (For a self-contained APK instead, build with `-p:EmbedAssembliesIntoApk=true`.)
- Deploying **updates the app in place** — the on-device data files (`files/*.json`, `shared_prefs/`) persist across a redeploy; they're only lost on an uninstall.
- **Always deploy the build you just made to every emulator/simulator you're about to look at — never trust what's already installed.** An emulator keeps its last install indefinitely, so a months-old version sits there looking current and you end up reviewing code that isn't yours. Building alone doesn't install anything; it takes the `-t:Run` deploy. Verify the version rather than assuming: Android `adb -s <serial> shell dumpsys package khost.mobile | grep versionName`, iOS `xcrun simctl listapps <udid> | grep -A12 khost.mobile` (look at `CFBundleVersion`, which is `ApplicationVersion` in the csproj).
  - iOS simulator: `dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-ios -t:Run "-p:BaseOutputPath=./obj/_build" "-p:_DeviceName=:v2:udid=<udid>"` (boot it first with `xcrun simctl boot <udid>`; `xcrun simctl list devices available` lists them).
- **A cold iOS build looks hung at `actool` — always run it with the output going to a log, and watch the log instead of the terminal.** `_CoreCompileImageAssets` shells out to `xcrun actool` to compile the asset catalog, and actool prints *nothing* for minutes while it works, so a foreground build sits silent long enough to look dead and get killed — which is how you end up with a simulator still running last month's version. Run it detached and tail the log (`*.log` is gitignored):
  ```bash
  dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-ios "-p:BaseOutputPath=./obj/_build" -v:n > ios-build.log 2>&1 &
  tail -f ios-build.log     # last line names the phase; "external tool execution … actool" = still working, not stuck
  ```
  A genuinely stuck actool is a different signature — see the Gotchas entry on Xcode components (`ibtoold failed IDE initialization`); check `ps -Ao pid,etime,comm | grep -iE 'actool|ibtool'` to tell a working actool from an absent one.
- **`-t:Run` can stall on the iOS simulator even when the build is fine.** If the build log reaches `Build succeeded` but nothing installs, skip MSBuild's deploy and install the bundle directly — safe on iOS (unlike Android's Fast Deployment, the simulator bundle carries its own assemblies):
  ```bash
  xcrun simctl install <udid> KHost.Mobile/obj/_buildDebug/net10.0-ios/iossimulator-arm64/KHost.Mobile.app
  xcrun simctl launch  <udid> khost.mobile
  ```
- **ALWAYS back up the device's data before pushing a build to a live/physical device — no exceptions.** Run `dotnet run scripts/backup-device-data.cs -- backup` first (it lands a gitignored tarball in `device-backups/`). A redeploy *normally* keeps data, but a signing-key mismatch (a build from a different machine / regenerated debug keystore), a package-id change, or a troubleshooting uninstall silently forces an **uninstall + reinstall that wipes singers, song lists, tonight sets, venues and settings**. The backup is the only safety net — take it every time, restore with `… -- restore <file>` if a deploy wipes the device. See DEVELOPMENT.md → "Backing up on-device data" for the full flow.
- **`<MauiVersion>` is pinned to `10.0.80` in the csproj on purpose — don't "clean it up" back to the workload default.** The workload default (10.0.20) crashes immediately on launch on Android 16 / API 36 (a .NET 10 MAUI root-fragment regression: *"No view found for id … for fragment NavigationRootManager_ElementBasedFragment"*). If an Android launch crash reappears after a workload update, bump `MauiVersion` to the latest serviced `10.0.x` on NuGet and verify with `adb logcat` for the "No view found" FATAL.

### UI automation (`playwright/`)

To drive the running app's WebView (walk the tour, exercise a flow, screenshot), use the tools in `playwright/` rather than hand-rolling a client — `device/` (full Playwright) for a physical device, `emulator/` (raw CDP with real-touch `tap`/`swipeDown`) for the emulator, whose older WebView rejects Playwright's connect. **`playwright/README.md` is the canonical how-to** — attach flow, examples, and the on-device gotchas.

## Local features (current focus — no server yet)

The app ships **offline/local UI only**. All local data sits behind an interface with a device-backed JSON implementation, so a server-sync implementation can drop in later without UI changes. Every store is registered as a singleton in `MauiProgram`, is `SemaphoreSlim`-guarded with an in-memory cache, raises a `Changed` event to drive UI refresh, and swallows a corrupt file rather than crashing.

**Mobile shell** (`Components/Layout/`): mobile-first — sticky top app bar, scrolling content, fixed bottom tab bar (`NavMenu`) with two tabs — **Tonight** (the on-deck set) then **My Songs** (the wishlist). The bar is only rendered when the Tonight feature is enabled (`MainLayout` gates it on `IAppSettings.TonightEnabled`); with it off there's a single destination, so the whole bar is hidden and nav runs through the header ⋮ menu. On launch, `MySongs` does a one-time "smart landing" (via `IAppSession`): open onto Tonight when a set is queued, else stay on My Songs. `UpdateBanner` sits at the top when a newer release is available. Theme in `wwwroot/app.css`: design tokens + light/dark via `prefers-color-scheme`; brand accent violet `#7c3aed`.

**Pages** (`Components/Pages/`): `MySongs.razor` (route `/`), `Tonight.razor` (route `/tonight`), `Venues.razor` (route `/venues`), `Singers.razor` (route `/singers`), `Settings.razor`, `ImportExport.razor`, `About.razor`, `NotFound.razor`.

**My Songs wishlist** — a patron's on-device list of songs to sing.
- `Models/SongListItem.cs` — mutable, JSON-persisted entity: free-text title/artist, `Genre`/`Year`, per-song `Enjoyment` (1–5), `IsFavorite` (favorites float to top), `Performances` (the performance history + per-performance "how it went" ratings; `AverageHowItWent`/`LastSungAt` derived from it), `SongListItemStatus` (`WantToSing` → `Sang`), and reserved `LibrarySongId` for future online-library links. Legacy fields (`SungDates`, `Confidence`) are read/migrate-only.
- `Services/ISongListStore.cs` + `JsonFileSongListStore.cs` — the wishlist store (UI binds to the interface only).

**Tonight set list** — an on-deck set for the venue, on its own tab (`Tonight.razor`), kept separate from the wishlist so a song sung earlier today stays un-checked until checked off here. Checking a row off logs a performance through the shared `RatingPromptSheet` component (also used by My Songs' "Log performance"); tapping a row body opens the shared `SongDetailSheet` read-only (no Edit — the row's own ✓/✕ keep the set mechanics); the wishlist cards keep a 🎤 quick-add to line songs up for the set.
- `Models/TonightEntry.cs` — references a `SongListItem` by id; owns `Order`, `Completed`/`CompletedAt`, and `CompletedPerformanceId` (so an undo removes exactly the performance the check-off logged, even after restart).
- `Services/ITonightStore.cs` + `JsonFileTonightStore.cs`.

**Singers (multiple users, one device)** — several people can share one device, each with their own **My Songs** and **Tonight** set; the **Venues** list is shared. Casual switching — no login/PIN — via the header **avatar** (`Components/Layout/SingerChip.razor`, a "Who's singing?" switcher mirroring `VenueChip`) or the **Singers** page (`Singers.razor` + `SingerEditSheet.razor`, reached from the ⋮ menu below Venues). The active singer's color re-tints the whole app chrome by overriding the `--kh-primary` tokens on `<html>` (`wwwroot/js/singer.js`, called from `SingerChip`).
- `Models/Singer.cs` + `SingerColors`/`SingerGlyphs` (the pickers); `Services/ISingerStore.cs` + `JsonFileSingerStore.cs` (`singers.json`). `EnsureSeededAsync` creates a default "Me" on first run and **migrates the legacy single-user `song-list.json` / `tonight.json` into it**.
- The **active singer** lives on `IAppSession` (`ActiveSingerId` / `ActiveSingerChanged`), remembered via `IAppSettings.LastActiveSingerId`; `MainLayout` resolves it before any personal page loads.
- The **song-list and tonight stores are namespaced per singer** — `song-list-{id}.json` / `tonight-{id}.json` (see `SingerDataFiles`; the id is the **dash-less** GUID), reloading on `ActiveSingerChanged` and falling back to the legacy file when no session is wired (the integration-test path).
- **Each singer keeps their own My Songs view** — `IAppSession.MySongsViewFor(singerId)` + `scroll.js` keyed `mysongs:{singerId}` restore that person's filters, sort and scroll on switch.
- **Row gestures are one JS module** — `wwwroot/js/swipe.js` owns tap / press-and-hold / swipe-left for the song, venue and singer lists off one pointer state machine; per-list `options` name the `[JSInvokable]` methods and opt in/out of hold and swipe. **Press-and-hold sets the active venue / singer**, confirmed by `IHaptics` (named that, not `IHapticFeedback`, to avoid MAUI Essentials' same-named interface); since no assistive-tech gesture maps to a long press, both pages keep a reachable equivalent in their sheet.
- **Icon picker is shared** — `Components/IconPicker.razor`, the collapsible color-+-emoji picker; the singer editor uses color + glyph, the venue editor glyph-only.

**Ratings & history** — `Performance` (per-performance "how it went" 1–5 + optional note + date) lives inside `SongListItem.Performances`; editable after the fact from the history sheet. Separate per-song `Enjoyment` rating.

**Lyrics** — `Services/ILyricsCache.cs` + `JsonFileLyricsCache.cs` cache lyrics on device; lookups go through `KHost.Mobile.Clients/Lyrics/` (LRCLIB, keyless).

**Quick links & search** — `Services/YouTubeSearch.cs`, `SpotifySearch.cs`, and `ILinkLauncher`/`MauiLinkLauncher` open a song on YouTube/Spotify.

**Auto-fill** — `KHost.Mobile.Clients/Enrichment/ITunesTrackMetadataLookup.cs` fills release year + genre + cover-art URL (keyless iTunes Search API). `SongListItem.MetadataLookedUp` guards against re-spending a rate-limited call.

**Cover-art fallback** — `KHost.Mobile.Clients/Deezer/DeezerCoverArtLookup.cs` (keyless, `ICoverArtLookup`) is consulted **only when iTunes returns no cover** (its popularity-ranked search misses album deep cuts). **Art only** — Deezer's `release_date` is the digital-availability date, not the original release, so year/genre stay with iTunes. `SongListItem.ArtworkLookedUp` gates re-lookup; the parser accepts artist-name variants while still rejecting a wrong artist.

**Spelling suggestions** — when a lookup finds no exact match but one near-miss, the song carries `SuggestedTitle`/`SuggestedArtist` and a ⚠ appears beside its name; in the detail sheet that mark is a button that unfolds the "Did you mean …?" offer. The lookup that produces it is gated on `ShouldLookUp` (**not** on genre/year still being blank — a song can have complete metadata and a wrong title). iTunes offers the correction from the call it was already making; `Deezer/DeezerSpellingSuggestionLookup.cs` (keyless, `ISpellingSuggestionLookup`) is a fallback consulted **only when iTunes returned neither a match nor a suggestion**. It uses Deezer's **plain free-text** search, not the field-scoped `artist:"…" track:"…"` form the cover-art lookup uses — that one is exact-only and returns nothing for a typo. The matching bar lives in `Matching/TrackSimilarity.cs` + `TrackSuggestionFinder.cs`, shared by both sources; see DEVELOPMENT.md → Design notes for why it's set where it is.

**Album-art display** — `Services/IAlbumArtLoader` + `AlbumArtLoader` own the loaded-cover map (song id → `blob:` URL) and the `khAlbumArt` interop; My Songs and Tonight share it, so a cover fetched on one tab is already there on the other. Registered **scoped, not singleton** — it talks through `IJSRuntime`, which is scoped in Blazor Hybrid; a singleton captures a JS runtime that isn't attached to the WebView and every transfer silently fails. (Why `blob:` URLs at all: DEVELOPMENT.md → Design notes.)

**Import / export** — `ImportExport.razor` pulls songs from a public Spotify or YouTube Music playlist link, or a KHost Cue `.json` file, and exports the whole list back out (`KHost.Mobile.Clients/Spotify/`, `YouTubeMusic/`).

**Update alert** — `Services/IAppUpdateService.cs` + `MauiAppUpdateService.cs` check the app's public GitHub Releases (`KHost.Mobile.Clients/Updates/`, anonymous) once per session; if a newer version exists, `UpdateBanner` offers a one-tap link. Disable-able in Settings; failures are swallowed (treated as "nothing new").

**Settings** — `Services/IAppSettings.cs` + `MauiAppSettings.cs` back a Settings screen where every extra behavior can be toggled (auto-fill, YouTube/Spotify links, lyrics, lyrics caching, Tonight, scroll-to-favorited, Surprise me, per-performance rating, update checks) plus a danger zone (clear lyrics cache / album art / song list). Beyond the on/off flags it also holds the app's **tunables** — values that used to be hardcoded constants: the undo window, launch destination, favorites-float and delete-confirm behavior, the rating prior weight and recency half-life, venue detection radius and history length, the spelling-suggestion level, catalogue region, import lookup delay, haptics and 12/24-hour time. Every default reproduces the behavior that constant had, so a fresh install is unchanged. **The Surprise draw rules are mirrored here as well as on the 🎲's press-and-hold sheet** — a long press has no assistive-tech equivalent, so the sheet alone would strand them.

## Gotchas

- **iOS cannot build on Windows** without a paired Mac. A bare `dotnet build` on the solution surfaces iOS/Apple-toolchain errors that are **not** your code. Build the **Android head explicitly** to verify, and use the **Windows head** for fast UI iteration. iOS is validated when a Mac is in the loop.
- **`TargetFrameworks` is `android;ios` + `windows` on Windows + `maccatalyst` on macOS** (tizen dropped). Don't re-add heads without a reason. Note that **restore evaluates every TFM even when you pass `-f`**, so a build of any single head fails until *all* the declared workloads are installed — `dotnet workload restore KHost.Mobile/KHost.Mobile.csproj` installs exactly the set the project declares.
- **The Mac Catalyst head needs full Xcode, not Command Line Tools.** `xcode-select -p` must point at `/Applications/Xcode.app/Contents/Developer` (set with `sudo xcode-select -s …`), the license must be accepted (`sudo xcodebuild -license accept` — note `xcodebuild -version` succeeds *without* it, so it's not a valid check; read `IDEXcodeVersionForAgreedToGMLicense` from `/Library/Preferences/com.apple.dt.Xcode` instead), and `xcodebuild -runFirstLaunch` must have installed the extra components or `actool` fails with `ibtoold failed IDE initialization`. All three surface as errors that look like build breakage but aren't.
- **The Mac Catalyst head is a layout preview, not a product** — see DEVELOPMENT.md → Design notes. Don't add desktop breakpoints, a side rail, or hover affordances "for the desktop app": there isn't one, and a wide window looking wrong is expected.
- This repo **builds standalone** — it no longer references the sibling `KHost.Online`/`KHost.Contracts` projects. (They return with the online slice; see Roadmap.)
- **`FileSystem.AppDataDirectory` is the `Data` SUBFOLDER**, i.e. `%LOCALAPPDATA%\KHost\khost.mobile\Data\` on unpackaged Windows (parent folders are the appxmanifest `PublisherDisplayName` = `KHost` and the `ApplicationId` = `khost.mobile`) — NOT the parent `khost.mobile\`. Seeding/inspecting persisted state must target `Data\`. Builds from *before* the publisher/id rename wrote to the legacy `%LOCALAPPDATA%\User Name\com.companyname.khost.mobile\Data\`; that stale copy is ignored by current builds.
- The template's `Components/Routes.razor` has `FocusOnNavigate Selector="h1"`; pages here have no `<h1>`, so nothing auto-focuses. Harmless, but don't rely on autofocus.
- **UI test automation on the Windows head is flaky** — WebView2 swallows the *first* SendKeys burst after launch, and page scroll position varies between launches (fixed click coords drift). For persistence checks, prefer seeding/reading the JSON files under `Data\` directly over driving the form. To drive the UI itself, use the Playwright tools in `playwright/` (see Commands → UI automation) — attaching over CDP with real locators sidesteps the SendKeys/coordinate flakiness.
- **Sample import data** — this public YouTube Music playlist imports cleanly via Import & Export → YouTube Music, handy for populating the list: `https://music.youtube.com/playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM`

## Conventions

The root `.editorconfig` encodes the mechanical rules (4-space indent, file-scoped namespaces, `_camelCase` private fields, `Async` suffix). Below is the intent and the patterns it can't express — match the surrounding code.

### Language & style
- File-scoped namespaces; **folder = namespace**.
- `sealed` on every concrete type (classes, records, exceptions).
- `sealed record` (positional) for DTOs / value types; **mutable `class` for JSON-persisted, editable entities** (mirrors KHost — carry a one-line rationale comment).
- **Primary constructors** for DI and exceptions; use the injected parameter by name — don't copy it to a field.
- Modern C#: collection expressions `[]`, target-typed `new()`, switch expressions, pattern matching, expression-bodied *one-liners*. `var` when the type is obvious.
- Private fields `_camelCase` (`readonly` where possible); constants `PascalCase`.

### Async
- `Async` suffix on every Task-returning method.
- Library / network methods take a trailing `CancellationToken cancellationToken = default` and thread it through every await.
- `ConfigureAwait(false)` in `KHost.Mobile.Clients` and other non-UI/background code. **Intentional exception:** the UI-thread JSON stores omit it — they rely on the Blazor sync context.
- Network calls use the filter idiom `catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)`, calling `cancellationToken.ThrowIfCancellationRequested()` first, then rethrowing a domain exception. Guard args with `ArgumentNullException.ThrowIfNull`.

### Errors, logging & docs
- Best-effort external ops (link launch, update check) swallow all exceptions and degrade gracefully; a "no result found" returns `null`, never throws — only genuine network/HTTP failures throw a domain exception.
- **Logging** lives in the MAUI head (`KHost.Mobile`), never in `KHost.Mobile.Clients` (which stays pure BCL — see the client-backend pattern). Inject `ILogger<T>` via the primary constructor; on the JSON stores make it an **optional, defaulted** parameter (`ILogger<T>? logger = null`) with a `NullLogger<T>.Instance` fallback field so the integration tests can `new` them without a logging stack. Use **structured** messages (named placeholders, not interpolation): `_log.LogDebug("… {Count} … {Path}", count, path)`. Levels: `Debug` for routine flow (store load/save, a lookup's start/outcome), `Information` for notable one-offs (an import count, every outbound HTTP request/response), `Warning` for a swallowed/degraded failure (corrupt file, failed cover download, a lookup that will retry). All network goes through `LoggingHttpMessageHandler` (`Diagnostics/`), chained onto every typed client in `MauiProgram` — the one seam that captures what the **native** platform HTTP stack actually sent/received on-device. Providers + filters are `#if DEBUG`-gated in `MauiProgram` (Debug build → logcat; Release stays quiet).
- Interfaces carry the substantive `<summary>`; implementations use `/// <inheritdoc />` plus a `<remarks>` for operational notes (rate limits, backends). Document positional record params and enum members. Inline `//` comments explain **why**, not what.

### Pattern: local store
- `IFooStore` interface + `JsonFileFooStore` impl (platform-backed services use the `MauiFoo` prefix instead), registered **singleton** in `MauiProgram`.
- `private readonly SemaphoreSlim _gate = new(1, 1)` + a nullable in-memory cache field as the source of truth. Every public method: `await _gate.WaitAsync(); try { … } finally { _gate.Release(); }`. Private `LoadAsync`/`SaveAsync` assume the caller already holds the gate (say so in a comment).
- Fire `Changed?.Invoke(this, EventArgs.Empty)` **after** releasing the gate, and only when something actually changed.
- One JSON file per store under `FileSystem.AppDataDirectory`; serialize with a **System.Text.Json source-gen** `JsonSerializerContext`. Swallow a corrupt file (`catch (JsonException)`) → empty state rather than crash.

### Pattern: client backend (`KHost.Mobile.Clients`)
- Stays **MAUI-free with zero package references** (pure BCL). One feature per folder/namespace.
- `HttpClient` is **injected** via primary constructor (never `new`); base address/headers are configured at DI registration, not in the library. Registered as a typed client (`AddHttpClient<IFace, Impl>`).
- Isolate parsing in a `static` "pure — no network" parser class; the service does HTTP + error mapping only.
- One `sealed` exception per feature: `sealed class FooException(string message, Exception? inner = null) : Exception(message, inner)`, with messages written to be shown in the UI. Deserialize via manual `JsonDocument` traversal (no reflection serializer here — that's the host's convention, not the client's).

### Pattern: Blazor component
- Single-file `.razor` (no code-behind, no scoped `.razor.css`; all CSS in `wwwroot/app.css`). Keep components single-purpose. `@inject` (never `[Inject]`); injected services get short semantic field names (`Store`, `Settings`, `JS`). `[Parameter]` props get `<summary>` docs.
- Load data in `OnInitializedAsync`, subscribe to store `Changed`, and implement `IDisposable` to unsubscribe. `async Task` handlers — never `async void`; fire-and-forget is an explicit `_ = FooAsync()` with an internal try/catch. `InvokeAsync(StateHasChanged)` from async continuations; bare `StateHasChanged()` from sync / `[JSInvokable]` paths.
- JS interop only in `OnAfterRenderAsync`: one `wwwroot/js/<feature>.js` per feature exposing `window.kh<Feature>.register(...)`, bound once via a `_xBound` flag; C#↔JS round-trips use `DotNetObjectReference` + `[JSInvokable]`.
- **Bottom sheets wrap `Components/Sheet.razor`** — it owns the backdrop, ✕, pull-down-to-dismiss and the page-scroll lock (read from the DOM, so stacked sheets can't strand it). Don't hand-roll `khSheet.register` / `setLock` in a page; pass `Open`/`OnClose` (and `OnSwipeDismiss` when a pull-down means something other than close — see `RatingPromptSheet`).
- CSS: `--kh-` design tokens in `:root`; light/dark via `@media (prefers-color-scheme)` plus a `[data-theme]` override; BEM class naming (`block__element`, `block--modifier`, `is-`/`active` state).

### Housekeeping
- **Do NOT commit or push unless explicitly asked.**
- **Both test suites (`KHost.Mobile.UnitTests` + `KHost.Mobile.IntegrationTests`) must pass before any commit or push.** Run them and only proceed once green — never commit or push with a failing (or unrun) suite:
  - `dotnet test KHost.Mobile.UnitTests/KHost.Mobile.UnitTests.csproj "-p:BaseOutputPath=./obj/_build"` — pure, no-I/O logic (parsers, `Genres`, `SongListItem`).
  - `dotnet test KHost.Mobile.IntegrationTests/KHost.Mobile.IntegrationTests.csproj "-p:BaseOutputPath=./obj/_build"` — the JSON stores against a real temp folder (real file I/O + serialization) via a fake `IAppDataDirectory`.
- **Keep the docs in sync with the app.** `README.md` is product-facing — update its feature list (and the screenshot grid where relevant) whenever a user-facing feature is added or its behavior changes, so it never lags the app. **[DEVELOPMENT.md](DEVELOPMENT.md)** holds the developer-facing docs — build/test commands, the screenshot target size, and the **Design notes** section; put design rationale for a non-obvious implementation (a new reusable component, a storage/serving decision, a platform workaround) there, not in the README.
- **`/research/` is gitignored and must never be committed** — it holds local planning/research notes and scratch data. Don't stage it, don't offer to commit it, and don't propose removing it from `.gitignore`.
- Secrets via user-secrets/config — never hard-coded or committed.

## Roadmap (server integration — deferred)

Eventual slice: **join venue → search library → request song → live queue** against KHost.Online. When it lands, `KHost.Mobile.Clients` gains the typed HTTP + `HubConnection` server client and re-takes a `KHost.Contracts` reference (removed for now so this repo builds standalone). `KHost.Online`'s REST slice is scaffolded and runtime-verified in its own repo.

Server repo: `../KHost.Online` (see its own `CLAUDE.md`). Planned first-slice REST + SignalR surface, all route strings in `KHost.Contracts/Routes.cs`:

| Method | Route (`Routes.Api.*`) | Purpose |
|---|---|---|
| POST | `/api/venues/join` | join code + display name → session token (`Bearer`) |
| GET | `/api/songs/search?q=` | filtered library (auth required) |
| POST | `/api/queue/request` | add self to queue for a song; broadcasts `QueueUpdated` |
| GET | `/api/queue` | current queue snapshot |
| Hub | `/hubs/queue` | SignalR push — connect with `?access_token=<sessionToken>` |

Planned auth (first slice): an opaque server-side session token sent as `Authorization: Bearer <token>` — a deliberate stand-in for a signed JWT later. Demo venue join code is `DEMO`. `IQueueClient` push methods: `QueueUpdated(queue)`, `NowPlayingChanged(nowPlaying?)`, `YoureUpNext()`.
