# AGENTS.md

Guidance for AI coding agents (Claude Code, GitHub Copilot, Cursor, etc.) working in this repository. `CLAUDE.md` imports this file, so this is the single source of truth.

**KHost.Mobile** (app name **"KHost Cue"**) — the singer/patron-facing companion app for [KHost](../KHost), the desktop karaoke host in a sibling repo. A **.NET MAUI Blazor Hybrid** app (iOS + Android) on **.NET 10**.

**The app is local/offline only** — a personal, on-device karaoke wishlist and "tonight" set list. No first-party server; the only network calls are the keyless third-party lookups in `KHost.Mobile.Clients`. Don't design toward a server. **This repo builds standalone** — never add a reference to the sibling `KHost` repo or consume its `Abstractions`/Domain/EF from mobile.

## Solution / project layout

`KHost.Mobile.slnx` — mobile keeps its OWN solution so MAUI workloads never slow the desktop builds. Eight projects (six under `src/`, two under `tests/`), layered so **only `KHost.Mobile` knows MAUI exists**. Dependency direction: `KHost.Mobile` → `KHost.Mobile.UI` → Abstractions/Common/Infrastructure/Clients. **The full type inventory lives in the wiki's [Architecture](https://github.com/riddlemd/KHost.Mobile/wiki/Architecture) page — it goes stale here.** What follows are the rules per project:

| Project | References | Rules |
|---|---|---|
| `KHost.Mobile.Abstractions` | **nothing** | Every contract, model, DTO and exception — nothing that implements one. Zero packages. **Adding a reference here is almost always the wrong fix** — its emptiness is what lets tests and every implementation project depend on the app's shape without dragging in MAUI or a backend. |
| `KHost.Mobile.Common` | **nothing** | Generic, domain-free helpers. **The bar: nothing here may know what a track, singer, venue, or backend is** — domain-shaped helpers belong in Clients or Infrastructure. Pure BCL, public API. |
| `KHost.Mobile.Infrastructure` | Abstractions | Every MAUI-free implementation, all under `Services/` (plus `Serialization/` = the `JsonSerializerContext` partials, `Diagnostics/` = `LoggingHttpMessageHandler`). Each is `internal sealed` behind its Abstractions interface, `<InternalsVisibleTo>` for both test projects; `JsonFileStore<T>` stays public for subclassing. Anything touching `Microsoft.Maui.*` does **not** belong here. |
| `KHost.Mobile.Clients` | Abstractions, Common, `Microsoft.Extensions.Http` | The vendor backends (one folder per vendor) plus the internal `Matching/` helpers. Every backend is `internal sealed`; nothing here is named outside `Project.cs`. MAUI-free, no logging of its own. |
| `KHost.Mobile.UI` | Abstractions, Common, Infrastructure, Clients | A plain `net10.0` Razor Class Library — **no MAUI workload**, so a component referencing a MAUI type is a compile error, not a convention. |
| `KHost.Mobile` | all of the above | The only project with the MAUI workload: `MauiProgram.cs`, `Platforms/`, `wwwroot/`, and `Services/` for the `Maui*` platform adapters. `MauiProgram` calls the three `AddKHost*()` extensions rather than registering types itself. **`ApplicationId` (`khost.mobile`) identifies the app to a device — changing it turns an update into an uninstall that wipes user data.** |
| `KHost.Mobile.UnitTests` / `.IntegrationTests` | all except `KHost.Mobile` (`.IntegrationTests` also skips `.UI`) | Plain `net10.0`; `<InternalsVisibleTo>` is what lets a test `new` an internal implementation. `UnitTests` references `.UI` **only** so `AsyncNamingConventionTests` can reflect over components — there are no component tests. The test projects have their own `Infrastructure`/`Clients` namespaces, so reaching a same-named production namespace needs `global::KHost.Mobile.…`. |

> **A namespace names its assembly: `<project>.<folder path>`** — no project sets `RootNamespace`. Consequence: an interface and its implementation sit in **different** namespaces (`Abstractions.Services.ISongListStore` vs `Infrastructure.Services.JsonFileSongListStore`), so a consumer of both imports both. Intended — a `using` that hid which assembly a type came from is what made the layering invisible.

## Commands

```bash
# Android head — THE green signal on Windows (iOS cannot build there; see gotcha).
dotnet build src/KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Windows / Mac Catalyst heads — fastest UI iteration. Catalyst is DEV-ONLY (layout preview):
# there is no desktop product, so no desktop breakpoints/side rails/hover affordances.
dotnet run --project src/KHost.Mobile -f net10.0-windows10.0.19041.0
dotnet run --project src/KHost.Mobile -f net10.0-maccatalyst

# Build, deploy AND launch on a connected Android device or emulator.
dotnet build src/KHost.Mobile/KHost.Mobile.csproj -f net10.0-android -t:Run "-p:BaseOutputPath=./obj/_build"

# Test suites — BOTH must pass before any commit or push, never with a failing or unrun suite.
dotnet test tests/KHost.Mobile.UnitTests/KHost.Mobile.UnitTests.csproj "-p:BaseOutputPath=./obj/_build"
dotnet test tests/KHost.Mobile.IntegrationTests/KHost.Mobile.IntegrationTests.csproj "-p:BaseOutputPath=./obj/_build"
```

`-p:BaseOutputPath=./obj/_build` redirects output so builds don't lock VS's `bin/`. The full command reference — workload setup, iOS-simulator deploy, multi-device targeting, build-verification — is the wiki's [Building & testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing); look it up when needed.

### Device-deploy rules

- **Deploy the Debug build with `-t:Run`, never `adb install` the APK.** Fast Deployment keeps the assemblies outside the APK; a bare install crashes at launch with *"No assemblies found in '…/.__override__/…'"*. (`-p:EmbedAssembliesIntoApk=true` for a self-contained APK.)
- **ALWAYS back up device data before pushing a build to a physical device — no exceptions.** `dotnet run scripts/backup-device-data.cs -- backup` first (restore with `… -- restore <file>`). A redeploy normally keeps data, but a signing-key mismatch, package-id change, or troubleshooting uninstall silently wipes singers, song lists, tonight sets, venues and settings — the backup is the only safety net.
- **Deploy the build you just made to every emulator you're about to look at.** An emulator keeps its last install indefinitely; building alone installs nothing, so a months-old version sits there looking current. Verify, don't assume.
- **`<MauiVersion>` is pinned (`10.0.80`) on purpose — don't "clean it up" to the workload default**, which crashes at launch on Android 16 (*"No view found for id … NavigationRootManager_ElementBasedFragment"*). If that crash reappears after a workload update, bump to the latest serviced `10.0.x`.

### UI automation (`playwright/`)

To drive the running app's WebView, use the tools in `playwright/` — `device/` (full Playwright) for a physical device, `emulator/` (raw CDP; the emulator's older WebView rejects Playwright's connect). **`playwright/README.md` is the canonical how-to.**

- **A physical device is someone's actual phone: drive the app, change nothing else.** Never `adb shell monkey` (a random-input fuzzer that can flip device settings like auto-rotate) — use `foreground()` from `device/khdrive.mjs`. No `adb shell settings put`, orientation, or developer-option changes unless the request was explicitly about that. And back up device data before every deploy (above).
- **A screenshot bound for `docs/screenshots/` is shot against seeded sample data — never a real library**, and you look at every PNG before committing it. The venue sheet prints a venue's KaraFun Id in the clear, and that folder is public; it has already leaked a real one. (DEVELOPMENT.md → Screenshots.)

## Local features

All local data sits behind an interface with a device-backed JSON implementation; UI depends only on the interfaces. **The feature/file map is the wiki's [Architecture](https://github.com/riddlemd/KHost.Mobile/wiki/Architecture), [Data storage](https://github.com/riddlemd/KHost.Mobile/wiki/Data-Storage) and [External services](https://github.com/riddlemd/KHost.Mobile/wiki/Clients-Library) pages.** What follows are the invariants not visible from the code you happen to have open:

- **Tonight ≠ wishlist state** — deliberately separate, so a song sung earlier today stays un-checked until checked off there. `TonightEntry.CompletedPerformanceId` is what lets an undo remove exactly the performance the check-off logged, even after a restart — keep that linkage when touching either side.
- **Per-singer vs shared** — song list and tonight set are per singer (`song-list-{id}.json` / `tonight-{id}.json`, **dash-less** GUID via `ISingerFileNames`), reloading on `ActiveSingerChanged`, falling back to the unsuffixed file when no session is wired (the test path). **Venues and settings are shared.** Deleting a venue orphans its performance tags rather than deleting performances; a manual venue pick **pins** it against auto-detect.
- **Every press-and-hold keeps a reachable non-gesture equivalent** — no assistive-tech gesture maps to a long press. Row gestures live in one module, `wwwroot/js/swipe.js`; haptics go through `IHaptics` (named to avoid MAUI Essentials' `IHapticFeedback`).
- **Lookups are one-shot** — `SongListItem.MetadataLookedUp`/`ArtworkLookedUp` gate the calls so a rate-limited lookup is never re-spent; editing title/artist re-opens them. Deezer is a **fallback only** and supplies **art only** — its `release_date` is the digital-availability date, so year/genre stay with iTunes; its suggestion path must use plain free-text search (the field-scoped form is exact-only and returns nothing for a typo). The ordering lives in `SongEnricher` (Infrastructure): it returns what to apply and the caller writes it, so an abandoned lookup leaves nothing half-applied. `ImportExport` runs a *different*, metadata-only policy — don't fold it in. (Rationale: DEVELOPMENT.md → Design notes.)
- **Album art** — asking `IAlbumArtService` for a song's view is what starts the fetch; the `IntersectionObserver` cap is keyed on what's *visible*, not rendered — key it on the rendered set and eviction thrashes. Registered **scoped, not singleton**: `IJSRuntime` is scoped in Blazor Hybrid, and a singleton captures a runtime not attached to the WebView, silently failing every transfer.
- **Settings hold the tunables** — every former hardcoded constant lives on `IAppSettings`, and **every default must reproduce the behavior the constant had**, so a fresh install is unchanged. Best-effort features (update check, link launch) swallow failures and degrade.

## Gotchas

- **iOS cannot build on Windows** without a paired Mac — a bare solution build surfaces Apple-toolchain errors that are **not** your code. Build the Android head to verify; iOS is validated when a Mac is in the loop.
- **Restore evaluates every TFM even with `-f`**, so any single-head build fails until all declared workloads are installed: `dotnet workload restore src/KHost.Mobile/KHost.Mobile.csproj`.
- **The Mac Catalyst head needs full Xcode, not Command Line Tools** — `ibtoold failed IDE initialization` is the signature of a setup gap, not a code break; fixes in the wiki's Building & testing.
- **`FileSystem.AppDataDirectory` is the `Data` SUBFOLDER** (`…\khost.mobile\Data\` on unpackaged Windows, not the parent) — seeding/inspecting persisted state must target `Data\`.
- **UI automation on the Windows head is flaky** (WebView2 swallows the first SendKeys burst; scroll position drifts). Prefer seeding/reading the JSON under `Data\` for persistence checks, and the `playwright/` tools to drive the UI.
- **Sample import data** — a public YouTube Music playlist that imports cleanly: `https://music.youtube.com/playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM`

## Conventions

The root `.editorconfig` encodes the mechanical rules (4-space indent, file-scoped namespaces, `_camelCase` private fields, `Async` suffix). Below is the intent and the patterns it can't express — match the surrounding code.

### Language & style
- File-scoped namespaces; **folder = namespace**.
- `sealed` on every concrete type. `sealed record` (positional) for DTOs/value types; **mutable `class` for JSON-persisted, editable entities** (carry a one-line rationale comment).
- **Primary constructors** for DI and exceptions; use the injected parameter by name — don't copy it to a field.
- Modern C#: collection expressions `[]`, target-typed `new()`, switch expressions, pattern matching, expression-bodied *one-liners*. `var` when the type is obvious.

### Async
- **`Async` suffix on every Task-returning method — including passthroughs without the `async` keyword**, which the `.editorconfig` rule can't see. `AsyncNamingConventionTests` closes that gap by reflection — a violation is a failing test.
- **A `[JSInvokable]` name is a cross-language contract.** `swipe.js`/`art-visibility.js` hold method names as string-literal defaults, and a mismatch fails silently at runtime. Pass `nameof(FooAsync)` from the `register` call; keep the JS-side defaults in step by hand.
- Library/network methods take a trailing `CancellationToken cancellationToken = default`, threaded through every await.
- `ConfigureAwait(false)` in Clients and other non-UI code. **Intentional exception:** the UI-thread JSON stores omit it — they rely on the Blazor sync context.
- Network calls: `catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)`, calling `cancellationToken.ThrowIfCancellationRequested()` first, then rethrowing a domain exception. Guard args with `ArgumentNullException.ThrowIfNull`.

### Errors, logging & docs
- Best-effort external ops swallow all exceptions and degrade; "no result found" returns `null`, never throws — only genuine network/HTTP failures throw a domain exception.
- Logging lives in Infrastructure and the head; **Clients has none** (the host chains `LoggingHttpMessageHandler` on via `AddKHostClients`'s `configureHandler`) and **Abstractions never logs**. Inject `ILogger<T>` via primary constructor; on stores make it **optional and defaulted** (`ILogger<T>? logger = null` + `NullLogger<T>.Instance` fallback) so tests can `new` them bare. **Structured** messages, never interpolation. Levels: `Debug` routine flow, `Information` notable one-offs (imports, every HTTP request/response), `Warning` swallowed/degraded failures. Providers are `#if DEBUG`-gated in `MauiProgram` — Release stays quiet.
- Interfaces carry the substantive `<summary>`; implementations use `/// <inheritdoc />` plus `<remarks>` for operational notes. Inline `//` comments explain **why**, not what.

### Pattern: local store
- `IFooStore` (Abstractions) + `JsonFileFooStore` (`internal sealed`, Infrastructure), registered **singleton** in `Project.cs`. Platform-backed services use the `MauiFoo` prefix and stay in the head — **the `Maui` prefix is a claim that the type touches the workload; if it has no `Microsoft.Maui.*` dependency it is misfiled, not just misnamed.**
- **Derive from `JsonFileStore<T>`** — it owns the gate, cache, crash-safe write and corrupt-file quarantine; a subclass supplies `TypeInfo`, `Label` and `PathFor(key)` (per-singer stores also `CurrentKey`). Don't hand-roll `LoadAsync`/`SaveAsync`. If the in-memory shape isn't the stored list, use the two-parameter `JsonFileStore<TItem, TCache>` with `Project`/`Flatten` (`JsonFileLyricsCache` is the example).
- Every public method wraps its own work in `await Gate.WaitAsync(); try { … } finally { Gate.Release(); }` — the inherited `LoadAsync`/`SaveAsync` assume the caller holds `Gate`. Fire `RaiseChanged()` **after** releasing the gate, and only when something actually changed.
- Writes go through `IAtomicFileWriter.WriteAsync` (`.tmp` + rename); `JsonFileStore<T>` is its only production caller. A corrupt file degrades to empty state, but **quarantine the bad bytes (`.corrupt` sibling) — never overwrite them**; the sibling is the only route to recovery.
- **Never read the clock ambiently — inject `TimeProvider` (optional, defaulted) and call `GetLocalNow()`, never `GetUtcNow()`**: every persisted timestamp carries a local offset, and switching would shift new ones by the UTC offset (`TonightStoreClockTests.Stamps_local_time_not_UTC` guards it). Elapsed time via `GetTimestamp()`/`GetElapsedTime()`. **Same for randomness: inject `Random`, never `Random.Shared`.** Both are registered in `AddKHostInfrastructure`.
  - **Known gap:** `SongListItem`/`Singer`/`Venue.AddedAt` still default via `= DateTimeOffset.Now` in their initializers, which no injected clock reaches — a frozen-clock test sees real time there.
- **Adding is never an overwrite**: `JsonFileSingerStore` and `JsonFileVenueStore` re-key an incoming id that's already in use (an import carries the ids it was exported with; two rows on one id break `UpdateAsync`/`RemoveAsync`). A non-colliding id is kept — imports rely on that.
- **Register a new persisted type on its `JsonSerializerContext`** (`Infrastructure/Serialization/` — one folder to check). Missing it fails at **runtime** (`NotSupportedException`), surviving a green build; a nested type still needs its own `[JsonSerializable]` line if anything serializes it standalone.
- **A per-singer store saves to the singer its cache was loaded for** — `JsonFileStore<T>` enforces it (`SaveAsync` writes to `PathFor(_loadedFor)`, never a re-read session id), so a singer switch mid-operation can't cross-write files.
- **`JsonFileSongListStore` is deliberately registered twice** — as itself and as `ISongListStore` resolving to the same instance — so profile export/import and interface consumers share one cache. Collapsing it splits the cache and breaks import/export.

### Pattern: client backend (`KHost.Mobile.Clients`)
- **Contract and backend live in different projects**: `Abstractions/Clients/…` holds the vendor-neutral interface, result types and exception; `Clients/<Vendor>/` holds only implementations with their vendor prefix. A new backend is a new folder in Clients, never a change to Abstractions.
- The exception belongs to the **capability**, not the backend (`CoverArtLookupException`, not `DeezerCoverArtException`) — callers never learn which vendor answered. One `sealed` exception per feature, message written to be shown in the UI.
- `HttpClient` is **injected** (never `new`); base address/headers configured at DI registration in `Project.cs`. Isolate parsing in a `static` "pure — no network" parser class; the service does HTTP + error mapping only. Deserialize via manual `JsonDocument` traversal — no reflection serializer here.

### Pattern: `Project.cs`
- Each implementation project exposes exactly **one** DI extension in a file named `Project.cs` on a public static class `Project`: `AddKHostInfrastructure()`, `AddKHostClients(userAgent, configureHandler)`, `AddKHostUI()`. A new store or backend is wired in its own project's `Project.cs`, never in `MauiProgram`. (`userAgent` is passed in because the Clients library can't read the app's version; `configureHandler` is the logging seam.)
- Two types stay public on purpose despite the `internal sealed` rule: `BackButtonOverlayGuard` (components `new` it directly) and `JsonFileStore<T>` (subclassed from outside).

### Pattern: Blazor component
- **Code-behind: markup in `.razor`, C# in a sibling `.razor.cs` partial** (`public sealed partial class`). No `@code` blocks — an `@code` block is a rule violation. No scoped `.razor.css`; all CSS in `wwwroot/app.css`. `@inject` in the markup (never `[Inject]`), short semantic field names (`Store`, `Settings`, `JS`); `[Parameter]` props in the `.razor.cs` with `<summary>` docs.
  - **A `.razor.cs` inherits none of the markup's `@using`.** The `_Imports.razor` set is mirrored as `<Using Include=…>` in `KHost.Mobile.UI.csproj` — **add to both** when a new shared namespace appears.
  - **`@implements` moves to the partial; `@inherits` must STAY in the markup** (the Razor-generated half always declares a base class; `MainLayout` is the one affected component).
  - Formatting bites harder in a `.razor.cs`: IDE0055 + `TreatWarningsAsErrors` make indentation an `@code` block tolerated into a build error. `dotnet format whitespace <proj> --include <file>` fixes it.
- Load data in `OnInitializedAsync`, subscribe to store `Changed`, `IDisposable` to unsubscribe. `async Task` handlers — never `async void`; fire-and-forget is an explicit `_ = FooAsync()` with an internal try/catch. `InvokeAsync(StateHasChanged)` from async continuations; bare `StateHasChanged()` from sync/`[JSInvokable]` paths.
- JS interop only in `OnAfterRenderAsync`: one `wwwroot/js/<feature>.js` per feature exposing `window.kh<Feature>.register(...)`, bound once via a `_xBound` flag; round-trips via `DotNetObjectReference` + `[JSInvokable]`.
- **A pointer gesture module ends the gesture on `window`, not the element** — a press-and-hold opens an overlay over the row, so the release lands on the overlay; a container-scoped listener leaves the gesture stuck active.
- **Bottom sheets wrap `Components/Sheets/Sheet.razor`** and live beside it — it owns the backdrop, dismiss, scroll lock and stacking. **Never give a sheet a `z-index`** (`khSheet.restack()` assigns them); don't hand-roll `khSheet.register`/`setLock` — pass `Open`/`OnClose` (and `OnSwipeDismiss` when pull-down ≠ close).
- CSS: `--kh-` design tokens in `:root`; light/dark via `@media (prefers-color-scheme)` plus a `[data-theme]` override; BEM naming.

### Housekeeping
- **Do NOT commit or push unless explicitly asked**, and only with both test suites green (commands above).
- **A test file mirrors its subject's path and namespace** (`Clients/Apple/ITunesResponseParser.cs` → `Clients/Apple/ITunesResponseParserTests.cs`). New test with no obvious folder usually means the type is in the wrong project. Cross-cutting helpers (`HttpTestDoubles`, `TempAppDataDirectory`) stay at the project root in the root namespace.
- **Keep the docs in sync.** `README.md` is product-facing — update its feature list when user-facing behavior changes. **DEVELOPMENT.md** holds developer docs and the **Design notes** — put design rationale there, not in the README.
- **The [wiki](https://github.com/riddlemd/KHost.Mobile/wiki) is a SEPARATE repo (`KHost.Mobile.wiki.git`)** — a commit here never touches it, so it goes stale silently. Its user pages quote on-screen labels verbatim, so renaming a button or setting breaks them with nothing changed in this repo. **Verify a user page against the running app (via `playwright/`), not the source** — source-reading has produced confidently wrong pages. **No developer identifiers on a user page** (name settings by their on-screen label); the contributor pages are technical by design and defer to DEVELOPMENT.md.
- **`/research/` is gitignored and must never be committed** — don't stage it, don't offer to, don't propose un-ignoring it.
- Secrets via user-secrets/config — never hard-coded or committed.
