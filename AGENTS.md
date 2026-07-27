# AGENTS.md

Guidance for AI coding agents (Claude Code, GitHub Copilot, Cursor, etc.) working in this repository. `CLAUDE.md` imports this file, so this is the single source of truth.

**KHost.Mobile** (app name **"KHost Cue"**) — the singer/patron-facing companion app for [KHost](../KHost) (open-source karaoke host software). A **.NET MAUI Blazor Hybrid** app (iOS + Android) on **.NET 10**.

**The app is local/offline only** — a personal, on-device karaoke wishlist and "tonight" set list. It talks to no first-party server; the only network calls are the keyless third-party lookups in `KHost.Mobile.Clients`. Don't design toward a server: keep UI bound to the store interfaces and keep `KHost.Mobile.Clients` self-contained, and that's enough.

## Cross-repo topology

The desktop host lives in a **sibling repo under `repos/`**:

```
repos/
├── KHost/            PUBLIC — the desktop karaoke host (Blazor Server + Avalonia). Untouched by mobile.
└── KHost.Mobile/     THIS repo — the MAUI Blazor Hybrid app.
```

- **The mobile app references no other repo — it builds standalone.** Don't add a cross-repo reference.
- **Never** reference the host's `KHost.Abstractions`/`Domain`/EF from mobile — mobile never consumes the host's domain model directly.

## Solution / project layout

`KHost.Mobile.slnx` (mobile stays in its OWN solution so MAUI workloads never slow the desktop or server builds):

Seven projects — five shipping plus two test — layered so that **only `KHost.Mobile.UI` knows MAUI exists**. Each references only what sits above it:

| Project | References | Role |
|---|---|---|
| `KHost.Mobile.Abstractions` | **nothing** | Every contract, model, DTO and exception, and nothing that implements one: the app's store + platform interfaces (`Services/`), its entities (`Models/`), and the client capability contracts (`Clients/Metadata/`, `CoverArt/`, `Lyrics/`, `Updates/`, plus the Spotify/YouTubeMusic import interfaces and DTOs). Zero packages. **Adding a reference here is almost always the wrong fix** — it's what lets the tests, and one day a Razor Class Library, depend on the app's shape without dragging in MAUI or a single backend. |
| `KHost.Mobile.Common` | **nothing** | Generic, domain-free helpers: the `JsonDocument` extensions (`Json/`) and the release-tag → `Version` normalizer (`Versioning/`). **The bar is that nothing here may know what a track, a singer or a venue is** — the matching algorithms started here and moved to Clients for failing exactly that test. Pure BCL, no project references, no packages. Public API, not `InternalsVisibleTo` — a helper general enough to live here is general enough for any project to call. |
| `KHost.Mobile.Infrastructure` | Abstractions | Every MAUI-free implementation, foldered by kind: `Services/` = types that are injected and hold state (the JSON stores, `AppSession`, `AlbumArtCache`, `VenueLocator`, `BackButtonService`, `SafeAreaInsets`); `Logic/` = static, stateless helpers (`RatingScore`, `SurprisePicker`, `AtomicFile`, `SingerDataFiles`, `SingerProfileCodec`, `TimeFormat`); `Serialization/` = the six `JsonSerializerContext` partials; `Search/` = the link builders and URL parser; `Models/` = parameter records; `Diagnostics/` = `LoggingHttpMessageHandler`. **`Services/` vs `Logic/` is the injected/static line** — if it's `static`, it isn't a service. Anything touching `Microsoft.Maui.*` does **not** belong here. |
| `KHost.Mobile.Clients` | Abstractions, Common | The vendor backends — `Apple/` (iTunes metadata), `Deezer/` (cover-art + spelling-suggestion fallback), `LrcLib/` (lyrics), `GitHub/` (the Releases update check), and the Spotify/YouTubeMusic parsers — plus `Matching/`, the internal track similarity/normalization helpers they share. Every contract they satisfy lives in Abstractions, so nothing here is named outside `MauiProgram`. |
| `KHost.Mobile.UI` | all of the above | The MAUI Blazor Hybrid head. Razor components (`Components/`), the `Maui*` platform adapters, `AlbumArtService` (talks to the WebView), `QrScanPage`, `MauiProgram`. The only project with the MAUI workload — and **every file in `Services/` genuinely binds to it**; anything that doesn't belongs one layer down. |
| `KHost.Mobile.UnitTests` / `.IntegrationTests` | all except UI | Plain `net10.0`, so they reference projects rather than `<Compile Include>`-linking source. |

> **A namespace names its assembly: `<project>.<folder path>`.** `Infrastructure/Services/` is `KHost.Mobile.Infrastructure.Services`, `Abstractions/Clients/Metadata/` is `KHost.Mobile.Abstractions.Clients.Metadata`. No project sets `RootNamespace` — it defaults to the project name, which is what makes this hold. The consequence to expect: an interface and its implementation now sit in **different** namespaces (`Abstractions.Services.ISongListStore` vs `Infrastructure.Services.JsonFileSongListStore`), so a consumer of both imports both. That is the intended trade — one `using` that hid which assembly a type came from was what made the layering invisible.
>
> **`KHost.Mobile.UI` pins `AssemblyName` to `KHost.Mobile`** (its `RootNamespace` is left to default, so its namespaces are `KHost.Mobile.UI.*`). The pin keeps the built artifact identical across the project rename. `ApplicationId` (`khost.mobile`) is a separate setting again — it is what identifies the app to a device, and moving it turns an update into an uninstall that wipes user data.

> If a PWA build is ever wanted, extract the Razor components into a Razor Class Library — it would reference `KHost.Mobile.Abstractions` alone, which is what the layering above buys.

## Commands

```bash
# Android head — THE green signal on Windows (iOS cannot build here; see gotcha).
dotnet build KHost.Mobile.UI/KHost.Mobile.UI.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Windows / Mac Catalyst heads — fastest UI iteration, no emulator or simulator.
# Catalyst is DEV-ONLY (layout preview). There is no desktop product; don't treat it as a shipping target.
dotnet run --project KHost.Mobile.UI -f net10.0-windows10.0.19041.0
dotnet run --project KHost.Mobile.UI -f net10.0-maccatalyst

# Build, deploy AND launch on a connected Android device or emulator.
dotnet build KHost.Mobile.UI/KHost.Mobile.UI.csproj -f net10.0-android -t:Run "-p:BaseOutputPath=./obj/_build"

# Client library on its own
dotnet build KHost.Mobile.Clients/KHost.Mobile.Clients.csproj
```

`-p:BaseOutputPath=./obj/_build` mirrors the KHost repo convention (redirects output so it doesn't lock VS's `bin/`).

**The full command reference lives in the wiki — [Building & testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing):** first-time workload/SDK setup, iOS-simulator deploy, targeting one of several attached devices (`-p:AdbTarget=-s <serial>`), the cold-`actool` hang, the `-t:Run` simulator stall and its `simctl` fallback, and how to verify which build a device is actually running. Look it up when you need it. What stays below is the handful of rules that bite whatever you're doing.

### Device-deploy rules

- **Always deploy the Debug build with `-t:Run`, never `adb install` the APK.** The Debug config keeps the .NET assemblies *outside* the APK (Fast Deployment) and relies on the MSBuild deploy target to push them to the device's `files/.__override__/`; a bare `adb install` launches then **crashes** with *"No assemblies found in '.../.__override__/...'. Assuming this is part of Fast Deployment. Exiting."* `-t:Run` does the push and starts the activity. (For a self-contained APK instead, build with `-p:EmbedAssembliesIntoApk=true`.)
- **ALWAYS back up the device's data before pushing a build to a live/physical device — no exceptions.** Run `dotnet run scripts/backup-device-data.cs -- backup` first (it lands a gitignored tarball in `device-backups/`). A redeploy *normally* keeps data — `files/*.json` and `shared_prefs/` survive an in-place update — but a signing-key mismatch (a build from a different machine / regenerated debug keystore), a package-id change, or a troubleshooting uninstall silently forces an **uninstall + reinstall that wipes singers, song lists, tonight sets, venues and settings**. The backup is the only safety net — take it every time, restore with `… -- restore <file>` if a deploy wipes the device. See DEVELOPMENT.md → "Backing up on-device data" for the full flow.
- **Deploy the build you just made to every emulator/simulator you're about to look at — never trust what's already installed.** An emulator keeps its last install indefinitely, so a months-old version sits there looking current and you review code that isn't yours. Building alone installs nothing; it takes the `-t:Run` deploy. Verify rather than assume — the wiki page has the version-check commands.
- **`<MauiVersion>` is pinned to `10.0.80` in the csproj on purpose — don't "clean it up" back to the workload default.** The workload default (10.0.20) crashes immediately on launch on Android 16 / API 36 (a .NET 10 MAUI root-fragment regression: *"No view found for id … for fragment NavigationRootManager_ElementBasedFragment"*). If an Android launch crash reappears after a workload update, bump `MauiVersion` to the latest serviced `10.0.x` on NuGet and verify with `adb logcat` for the "No view found" FATAL.

### UI automation (`playwright/`)

To drive the running app's WebView (walk the tour, exercise a flow, screenshot), use the tools in `playwright/` rather than hand-rolling a client — `device/` (full Playwright) for a physical device, `emulator/` (raw CDP with real-touch `tap`/`swipeDown`) for the emulator, whose older WebView rejects Playwright's connect. **`playwright/README.md` is the canonical how-to** — attach flow, examples, and the on-device gotchas.

## Local features

All local data sits behind an interface with a device-backed JSON implementation; UI code depends only on the interfaces (the store pattern itself is under Conventions). **The full feature/file map — every page, store, model and client with its role — is the wiki's [Architecture](https://github.com/riddlemd/KHost.Mobile/wiki/Architecture), [Data storage](https://github.com/riddlemd/KHost.Mobile/wiki/Data-Storage) and [External services](https://github.com/riddlemd/KHost.Mobile/wiki/Clients-Library) pages; look the inventory up there.** What follows are the per-feature invariants that aren't visible from the code you happen to have open:

- **Two tabs, gated** — the bottom bar (`NavMenu`) renders only when `IAppSettings.TonightEnabled` is on; off, the whole bar hides (a one-tab bar made no sense) and nav moves to the header ⋮ menu. Brand accent violet `#7c3aed`; the active singer's color re-tints the chrome by overriding `--kh-primary` on `<html>` (`wwwroot/js/singer.js`).
- **Tonight ≠ wishlist state** — the set is deliberately separate so a song sung earlier today stays un-checked until checked off there. `TonightEntry.CompletedPerformanceId` exists so an undo removes exactly the performance the check-off logged, even after a restart — keep that linkage when touching either side.
- **Per-singer vs shared** — the song list and tonight set are namespaced per singer (`song-list-{id}.json` / `tonight-{id}.json`, **dash-less** GUID, see `SingerDataFiles`), reloading on `ActiveSingerChanged` and falling back to the legacy file when no session is wired (the integration-test path). **Venues and settings are shared** across singers. `JsonFileSingerStore.EnsureSeededAsync` seeds a default "Me" and migrates the legacy single-user files into it. Deleting a venue orphans its performance tags rather than deleting performances; a manual venue pick **pins** it so auto-detect won't override.
- **Row gestures are one JS module** — `wwwroot/js/swipe.js` owns tap / press-and-hold / swipe-left for the song, venue and singer lists off one pointer state machine; per-list `options` name the `[JSInvokable]` methods and opt in/out of hold and swipe. Haptics go through `IHaptics` (named that, not `IHapticFeedback`, to avoid MAUI Essentials' same-named interface). **Every press-and-hold keeps a reachable non-gesture equivalent** — no assistive-tech gesture maps to a long press. Same reason the Surprise draw rules appear in Settings *as well as* on the 🎲's press-and-hold sheet.
- **Lookups are one-shot** — `SongListItem.MetadataLookedUp` / `ArtworkLookedUp` gate the iTunes/Deezer calls so a rate-limited lookup is never re-spent; editing title/artist is what re-opens them. Deezer is a **fallback only** (art when iTunes has no cover; suggestions when iTunes offered neither match nor suggestion) and supplies **art only** — its `release_date` is the digital-availability date, so year/genre stay with iTunes. The suggestion path must use Deezer's plain free-text search: the field-scoped `artist:"…" track:"…"` form is exact-only and returns nothing for a typo. Spelling suggestions are gated on `ShouldLookUp`, **not** on genre/year being blank — complete metadata can still carry a wrong title. (Why the similarity bar sits where it does: DEVELOPMENT.md → Design notes.)
- **Album art** — asking `IAlbumArtService` for a song's view is what starts the fetch; an `IntersectionObserver` (`wwwroot/js/art-visibility.js`) bounds in-memory covers keyed on what's *visible*, not what's rendered — key it on the rendered set and eviction thrashes. Registered **scoped, not singleton**: it talks through `IJSRuntime`, which is scoped in Blazor Hybrid, and a singleton captures a JS runtime that isn't attached to the WebView so every transfer silently fails. (Why `blob:` URLs: DEVELOPMENT.md → Design notes.)
- **Settings hold the tunables** — every value that used to be a hardcoded constant (undo window, launch tab, rating prior weight, recency half-life, venue radius, import delay, …) lives on `IAppSettings`, and **every default must reproduce the behavior the constant had**, so a fresh install is unchanged. Best-effort features (update check, link launch) swallow failures and degrade.
- Legacy fields on `SongListItem` (`SungDates`, `Confidence`) are **read/migrate-only** — never write them.

## Gotchas

- **iOS cannot build on Windows** without a paired Mac. A bare `dotnet build` on the solution surfaces iOS/Apple-toolchain errors that are **not** your code. Build the **Android head explicitly** to verify, and use the **Windows head** for fast UI iteration. iOS is validated when a Mac is in the loop.
- **`TargetFrameworks` is `android;ios` + `windows` on Windows + `maccatalyst` on macOS** (tizen dropped). Don't re-add heads without a reason. Note that **restore evaluates every TFM even when you pass `-f`**, so a build of any single head fails until *all* the declared workloads are installed — `dotnet workload restore KHost.Mobile.UI/KHost.Mobile.UI.csproj` installs exactly the set the project declares.
- **The Mac Catalyst head needs full Xcode, not Command Line Tools** — three setup gaps (`xcode-select` path, unaccepted license, missing `-runFirstLaunch` components) all surface as errors that look like build breakage but aren't; `ibtoold failed IDE initialization` is the signature. The checks and fixes are in the wiki's [Building & testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing) — note `xcodebuild -version` succeeding is NOT proof the license is accepted.
- **The Mac Catalyst head is a layout preview, not a product** — see DEVELOPMENT.md → Design notes. Don't add desktop breakpoints, a side rail, or hover affordances "for the desktop app": there isn't one, and a wide window looking wrong is expected.
- This repo **builds standalone** — no references to any sibling repo. Don't add one.
- **`FileSystem.AppDataDirectory` is the `Data` SUBFOLDER** — `%LOCALAPPDATA%\KHost\khost.mobile\Data\` on unpackaged Windows, NOT the parent `khost.mobile\`. Seeding/inspecting persisted state must target `Data\`. (A stale legacy copy from before the publisher/id rename may exist under `com.companyname.khost.mobile`; current builds ignore it.)
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
- **Logging** lives in `KHost.Mobile.Infrastructure` and `KHost.Mobile.UI`, never in `KHost.Mobile.Clients` (which stays pure BCL — see the client-backend pattern) and never in `KHost.Mobile.Abstractions` (which takes no package reference at all). Inject `ILogger<T>` via the primary constructor; on the JSON stores make it an **optional, defaulted** parameter (`ILogger<T>? logger = null`) with a `NullLogger<T>.Instance` fallback field so the integration tests can `new` them without a logging stack. Use **structured** messages (named placeholders, not interpolation): `_log.LogDebug("… {Count} … {Path}", count, path)`. Levels: `Debug` for routine flow (store load/save, a lookup's start/outcome), `Information` for notable one-offs (an import count, every outbound HTTP request/response), `Warning` for a swallowed/degraded failure (corrupt file, failed cover download, a lookup that will retry). All network goes through `LoggingHttpMessageHandler` (`Diagnostics/`), chained onto every typed client in `MauiProgram` — the one seam that captures what the **native** platform HTTP stack actually sent/received on-device. Providers + filters are `#if DEBUG`-gated in `MauiProgram` (Debug build → logcat; Release stays quiet).
- Interfaces carry the substantive `<summary>`; implementations use `/// <inheritdoc />` plus a `<remarks>` for operational notes (rate limits, backends). Document positional record params and enum members. Inline `//` comments explain **why**, not what.

### Pattern: local store
- `IFooStore` interface (in `KHost.Mobile.Abstractions`) + `JsonFileFooStore` impl (in `KHost.Mobile.Infrastructure`), registered **singleton** in `MauiProgram`. Platform-backed services use the `MauiFoo` prefix instead and stay in `KHost.Mobile.UI` — the `Maui` prefix is the signal that a type touches the workload and therefore can't move down a layer.
- `private readonly SemaphoreSlim _gate = new(1, 1)` + a nullable in-memory cache field as the source of truth. Every public method: `await _gate.WaitAsync(); try { … } finally { _gate.Release(); }`. Private `LoadAsync`/`SaveAsync` assume the caller already holds the gate (say so in a comment).
- Fire `Changed?.Invoke(this, EventArgs.Empty)` **after** releasing the gate, and only when something actually changed.
- **Never read the clock ambiently — take `TimeProvider` and call `GetLocalNow()`.** `DateTimeOffset.Now`/`DateTime.Today` in a store or a component can't be frozen by a test. Inject it like `ILogger`: an optional, defaulted parameter (`TimeProvider? timeProvider = null`) falling back to `TimeProvider.System`, so tests can still `new` the store bare; `MauiProgram` registers `TimeProvider.System` and DI fills it in (asserted by `StoreClockInjectionTests` — an optional parameter silently falling back would otherwise be invisible). **`GetLocalNow()`, never `GetUtcNow()`**: every timestamp this app has persisted carries a local offset, and switching would shift new ones by the UTC offset — `TonightStoreClockTests.Stamps_local_time_not_UTC` is the guard. Elapsed time is `GetTimestamp()`/`GetElapsedTime()`, not `Environment.TickCount64`.
- **Same rule for randomness: inject `Random`, never reach for `Random.Shared`.** `MauiProgram` registers it beside the clock. `SurprisePicker` is pure because the roll is an *argument*, but that only makes the picker reproducible — a caller using the global RNG leaves the draw itself untestable. Injected, a test substitutes `new Random(seed)` and pins the whole flow.
- **The `Maui` prefix is a claim, and it must be true.** It means "this type touches the workload, so it cannot move below `KHost.Mobile.UI`". `MauiVenueLocator` carried the prefix while using nothing but `ILogger` and Abstractions — it was pure policy over `ILocationProvider`, and is now `VenueLocator` in Infrastructure. If a `Maui*` type has no `Microsoft.Maui.*` dependency, it is misfiled, not just misnamed.
  - **Known gap:** `SongListItem`/`Singer`/`Venue.AddedAt` still default via `= DateTimeOffset.Now` in their property initializers, which no clock can reach. A frozen-clock test that adds a song, singer or venue will see the real time in `AddedAt`. Fixing it means every creation site setting the timestamp explicitly.
- One JSON file per store under `FileSystem.AppDataDirectory`; write through `AtomicFile.WriteAsync` (`.tmp` + rename) so a kill mid-write can't truncate the file.
- A corrupt file (`catch (JsonException)`) degrades to empty state rather than crashing — but **move the bad bytes aside with `AtomicFile.Quarantine` (a `.corrupt` sibling), never overwrite them.** "Swallow" means the app keeps running, not that the user's data is discarded; the sibling is the only route to recovery.
- **Register a new persisted type on its `JsonSerializerContext`, and check the nested ones too.** Missing it fails at **runtime** (`NotSupportedException`), not at compile time, so it survives a green build — and a type nested inside another serialized type still needs its own `[JsonSerializable]` line if anything serializes it standalone. **The model and its context now live in different projects** (`Abstractions/Models/` and `Infrastructure/Serialization/`), so adding a model no longer puts the context in front of you — it is easier than ever to miss. The upside of the split: every context sits in one folder, so "did I register it?" is one directory to check.
- **A store that writes per-singer files must save to the singer its cache was loaded for** (`_loadedFor`, captured before any `await`) — never a freshly re-read `IAppSession.ActiveSingerId`. A singer switch landing mid-write would otherwise put one singer's songs in another's file. See `JsonFileSongListStore.SaveAsync`.
- **`JsonFileSongListStore` is deliberately registered twice** — once as itself, once as `ISongListStore` resolving to the same instance (`MauiProgram.cs`) — so the profile export/import path and every interface consumer share one cache. It is not a redundant registration; collapsing it splits the cache and breaks import/export.

### Pattern: client backend (`KHost.Mobile.Clients`)
- Stays **MAUI-free with zero package references** (pure BCL).
- **The contract and the backend live in different projects.** `Abstractions/Clients/Metadata|CoverArt|Lyrics|Updates/` hold only the interface, its result types and its exception — all vendor-neutral, so a second backend needs no edit to them. `Clients/Apple|Deezer|LrcLib|GitHub/` hold only implementations, and those classes keep their vendor prefix (`ITunesResponseParser`, `DeezerCoverArtLookup`). A new backend is a new folder in Clients, never a change to Abstractions.
- The exception belongs to the **capability**, not the backend that throws it (`CoverArtLookupException`, not `DeezerCoverArtException`) — callers catch it through an interface that deliberately hides which vendor answered.
- `HttpClient` is **injected** via primary constructor (never `new`); base address/headers are configured at DI registration, not in the library. Registered as a typed client (`AddHttpClient<IFace, Impl>`).
- Isolate parsing in a `static` "pure — no network" parser class; the service does HTTP + error mapping only.
- One `sealed` exception per feature: `sealed class FooException(string message, Exception? inner = null) : Exception(message, inner)`, with messages written to be shown in the UI. Deserialize via manual `JsonDocument` traversal (no reflection serializer here — that's the host's convention, not the client's).

### Pattern: Blazor component
- Single-file `.razor` (no code-behind, no scoped `.razor.css`; all CSS in `wwwroot/app.css`). Keep components single-purpose. `@inject` (never `[Inject]`); injected services get short semantic field names (`Store`, `Settings`, `JS`). `[Parameter]` props get `<summary>` docs.
- Load data in `OnInitializedAsync`, subscribe to store `Changed`, and implement `IDisposable` to unsubscribe. `async Task` handlers — never `async void`; fire-and-forget is an explicit `_ = FooAsync()` with an internal try/catch. `InvokeAsync(StateHasChanged)` from async continuations; bare `StateHasChanged()` from sync / `[JSInvokable]` paths.
- JS interop only in `OnAfterRenderAsync`: one `wwwroot/js/<feature>.js` per feature exposing `window.kh<Feature>.register(...)`, bound once via a `_xBound` flag; C#↔JS round-trips use `DotNetObjectReference` + `[JSInvokable]`.
- **A pointer gesture module ends the gesture on `window`, not the element** — `swipe.js` binds `pointerup`/`pointercancel` there on purpose. A press-and-hold opens an overlay *over* the row, so the release lands on the overlay; a container-scoped listener would never fire, leaving the gesture stuck active and killing every later gesture on that list.
- **Bottom sheets wrap `Components/Sheet.razor`** — it owns the backdrop, ✕, pull-down-to-dismiss and the page-scroll lock (read from the DOM, so stacked sheets can't strand it). Don't hand-roll `khSheet.register` / `setLock` in a page; pass `Open`/`OnClose` (and `OnSwipeDismiss` when a pull-down means something other than close — see `RatingPromptSheet`).
- CSS: `--kh-` design tokens in `:root`; light/dark via `@media (prefers-color-scheme)` plus a `[data-theme]` override; BEM class naming (`block__element`, `block--modifier`, `is-`/`active` state).

### Housekeeping
- **Do NOT commit or push unless explicitly asked.**
- **Both test suites (`KHost.Mobile.UnitTests` + `KHost.Mobile.IntegrationTests`) must pass before any commit or push.** Run them and only proceed once green — never commit or push with a failing (or unrun) suite:
  - `dotnet test KHost.Mobile.UnitTests/KHost.Mobile.UnitTests.csproj "-p:BaseOutputPath=./obj/_build"` — pure, no-I/O logic (parsers, `Genres`, `SongListItem`).
  - `dotnet test KHost.Mobile.IntegrationTests/KHost.Mobile.IntegrationTests.csproj "-p:BaseOutputPath=./obj/_build"` — the JSON stores against a real temp folder (real file I/O + serialization) via a fake `IAppDataDirectory`.
- **A test file mirrors its subject's path**: `Clients/Apple/ITunesResponseParser.cs` is tested by `Clients/Apple/ITunesResponseParserTests.cs`, with the namespace following the folder (`KHost.Mobile.UnitTests.Clients.Apple`). New test, no obvious folder? That usually means the type is in the wrong project. Cross-cutting **helpers** (`HttpTestDoubles`, `TempAppDataDirectory`) stay at the project root in the root namespace — nested test namespaces see them without a `using`, since C# resolution walks enclosing namespaces.
- **Keep the docs in sync with the app.** `README.md` is product-facing — update its feature list (and the screenshot grid where relevant) whenever a user-facing feature is added or its behavior changes, so it never lags the app. **[DEVELOPMENT.md](DEVELOPMENT.md)** holds the developer-facing docs — build/test commands, the screenshot target size, and the **Design notes** section; put design rationale for a non-obvious implementation (a new reusable component, a storage/serving decision, a platform workaround) there, not in the README.
- **The [wiki](https://github.com/riddlemd/KHost.Mobile/wiki) is a SEPARATE repo (`KHost.Mobile.wiki.git`) — a commit here never touches it, so it goes stale silently.** Clone it, edit the markdown, push. Its user pages describe the app screen by screen and **quote on-screen labels verbatim**, so renaming a button, a setting or its helper text breaks them even when nothing in this repo looks wrong. Two rules that keep it honest:
  - **Verify a user page against the running app, not the source.** Attach with the `playwright/` tools and read the actual rendered control. Source-reading produced a page claiming a 🎤 quick-add button (the real one is an icon tooltipped "Add to tonight"; 🎤 is the *tab*) and a six-column sort bar (it's one dropdown plus a direction button) — both obvious on screen, both invisible in the markup a reader of `.razor` reconstructs in their head.
  - **No developer identifiers on a user page** — no type names, settings property names, file names or CSS classes. Name the setting by its on-screen label (*"Trust a rating after"*, not `RatingPriorWeight`). The contributor pages (Architecture, Data storage, UI components, External services, Building & testing, Conventions) are the opposite: technical by design, and they defer to DEVELOPMENT.md rather than restating it.
- **`/research/` is gitignored and must never be committed** — it holds local planning/research notes and scratch data. Don't stage it, don't offer to commit it, and don't propose removing it from `.gitignore`.
- Secrets via user-secrets/config — never hard-coded or committed.
