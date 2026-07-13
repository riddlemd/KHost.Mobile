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
| `KHost.Mobile.Client` | Standalone client library — the outward-facing lookups: playlist import (`Spotify/`, `YouTubeMusic/`), iTunes metadata (`Enrichment/`), LRCLIB lyrics (`Lyrics/`), and the GitHub-Releases update check (`Updates/`). No MAUI dependency. |

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

## Local features (current focus — no server yet)

The app ships **offline/local UI only**. All local data sits behind an interface with a device-backed JSON implementation, so a server-sync implementation can drop in later without UI changes. Every store is registered as a singleton in `MauiProgram`, is `SemaphoreSlim`-guarded with an in-memory cache, raises a `Changed` event to drive UI refresh, and swallows a corrupt file rather than crashing.

**Mobile shell** (`Components/Layout/`): mobile-first — sticky top app bar, scrolling content, fixed bottom tab bar (`NavMenu`) with two tabs — **Tonight** (the on-deck set) then **My List** (the wishlist). The bar is only rendered when the Tonight feature is enabled (`MainLayout` gates it on `IAppSettings.TonightEnabled`); with it off there's a single destination, so the whole bar is hidden and nav runs through the header ⋮ menu. On launch, `MySongs` does a one-time "smart landing" (via `IAppSession`): open onto Tonight when a set is queued, else stay on My List. `UpdateBanner` sits at the top when a newer release is available. Theme in `wwwroot/app.css`: design tokens + light/dark via `prefers-color-scheme`; brand accent violet `#7c3aed`.

**Pages** (`Components/Pages/`): `MySongs.razor` (route `/`), `Tonight.razor` (route `/tonight`), `Settings.razor`, `ImportExport.razor`, `About.razor`, `NotFound.razor`.

**My Songs wishlist** — a patron's on-device list of songs to sing.
- `Models/SongListItem.cs` — mutable, JSON-persisted entity: free-text title/artist, `Genre`/`Year`, per-song `Enjoyment` (1–5), `IsFavorite` (favorites float to top), `Performances` (the sung-history + per-sing "how it went" ratings; `AverageHowItWent`/`LastSungAt` derived from it), `SongListItemStatus` (`WantToSing` → `Sang`), and reserved `LibrarySongId` for future online-library links. Legacy fields (`SungDates`, `Confidence`) are read/migrate-only.
- `Services/ISongListStore.cs` + `JsonFileSongListStore.cs` — the wishlist store (UI binds to the interface only).

**Tonight set list** — an on-deck set for the venue, on its own tab (`Tonight.razor`), kept separate from the wishlist so a song sung earlier today stays un-checked until checked off here. Checking a row off logs a performance through the shared `RatingPromptSheet` component (also used by My Songs' "Mark sung"); the wishlist cards keep a 🎤 quick-add to line songs up for the set.
- `Models/TonightEntry.cs` — references a `SongListItem` by id; owns `Order`, `Completed`/`CompletedAt`, and `CompletedPerformanceId` (so an undo removes exactly the performance the check-off logged, even after restart).
- `Services/ITonightStore.cs` + `JsonFileTonightStore.cs`.

**Ratings & history** — `Performance` (per-sing "how it went" 1–5 + optional note + date) lives inside `SongListItem.Performances`; editable after the fact from the history sheet. Separate per-song `Enjoyment` rating.

**Lyrics** — `Services/ILyricsCache.cs` + `JsonFileLyricsCache.cs` cache lyrics on device; lookups go through `KHost.Mobile.Client/Lyrics/` (LRCLIB, keyless).

**Quick links & search** — `Services/YouTubeSearch.cs`, `SpotifySearch.cs`, and `ILinkLauncher`/`MauiLinkLauncher` open a song on YouTube/Spotify.

**Auto-fill** — `KHost.Mobile.Client/Enrichment/ITunesTrackMetadataLookup.cs` fills release year + genre (keyless iTunes Search API). `SongListItem.MetadataLookedUp` guards against re-spending a rate-limited call.

**Import / export** — `ImportExport.razor` pulls songs from a public Spotify or YouTube Music playlist link, or a KHost Cue `.json` file, and exports the whole list back out (`KHost.Mobile.Client/Spotify/`, `YouTubeMusic/`).

**Update alert** — `Services/IAppUpdateService.cs` + `MauiAppUpdateService.cs` check the app's public GitHub Releases (`KHost.Mobile.Client/Updates/`, anonymous) once per session; if a newer version exists, `UpdateBanner` offers a one-tap link. Disable-able in Settings; failures are swallowed (treated as "nothing new").

**Settings** — `Services/IAppSettings.cs` + `MauiAppSettings.cs` back a Settings screen where every extra behavior (auto-fill, YouTube/Spotify links, lyrics, lyrics caching, Tonight, scroll-to-favorited, Surprise me + skip-today, per-performance rating, update checks) can be toggled, plus a danger zone (clear lyrics cache / clear song list).

## Gotchas

- **iOS cannot build on Windows** without a paired Mac. A bare `dotnet build` on the solution surfaces iOS/Apple-toolchain errors that are **not** your code. Build the **Android head explicitly** to verify, and use the **Windows head** for fast UI iteration. iOS is validated when a Mac is in the loop.
- **`TargetFrameworks` is trimmed to `android;ios;windows`** (maccatalyst/tizen dropped) since the stated targets are iOS/Android. Don't re-add heads without a reason.
- This repo **builds standalone** — it no longer references the sibling `KHost.Online`/`KHost.Contracts` projects. (They return with the online slice; see Roadmap.)
- **`FileSystem.AppDataDirectory` is the `Data` SUBFOLDER**, i.e. `%LOCALAPPDATA%\KHost\khost.mobile\Data\` on unpackaged Windows (parent folders are the appxmanifest `PublisherDisplayName` = `KHost` and the `ApplicationId` = `khost.mobile`) — NOT the parent `khost.mobile\`. Seeding/inspecting persisted state must target `Data\`. Builds from *before* the publisher/id rename wrote to the legacy `%LOCALAPPDATA%\User Name\com.companyname.khost.mobile\Data\`; that stale copy is ignored by current builds.
- The template's `Components/Routes.razor` has `FocusOnNavigate Selector="h1"`; pages here have no `<h1>`, so nothing auto-focuses. Harmless, but don't rely on autofocus.
- **UI test automation on the Windows head is flaky** — WebView2 swallows the *first* SendKeys burst after launch, and page scroll position varies between launches (fixed click coords drift). For persistence checks, prefer seeding/reading the JSON files under `Data\` directly over driving the form.
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
- `ConfigureAwait(false)` in `KHost.Mobile.Client` and other non-UI/background code. **Intentional exception:** the UI-thread JSON stores omit it — they rely on the Blazor sync context.
- Network calls use the filter idiom `catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)`, calling `cancellationToken.ThrowIfCancellationRequested()` first, then rethrowing a domain exception. Guard args with `ArgumentNullException.ThrowIfNull`.

### Errors, logging & docs
- Best-effort external ops (link launch, update check) swallow all exceptions and degrade gracefully; a "no result found" returns `null`, never throws — only genuine network/HTTP failures throw a domain exception. No `ILogger` in the services layer today.
- Interfaces carry the substantive `<summary>`; implementations use `/// <inheritdoc />` plus a `<remarks>` for operational notes (rate limits, backends). Document positional record params and enum members. Inline `//` comments explain **why**, not what.

### Pattern: local store
- `IFooStore` interface + `JsonFileFooStore` impl (platform-backed services use the `MauiFoo` prefix instead), registered **singleton** in `MauiProgram`.
- `private readonly SemaphoreSlim _gate = new(1, 1)` + a nullable in-memory cache field as the source of truth. Every public method: `await _gate.WaitAsync(); try { … } finally { _gate.Release(); }`. Private `LoadAsync`/`SaveAsync` assume the caller already holds the gate (say so in a comment).
- Fire `Changed?.Invoke(this, EventArgs.Empty)` **after** releasing the gate, and only when something actually changed.
- One JSON file per store under `FileSystem.AppDataDirectory`; serialize with a **System.Text.Json source-gen** `JsonSerializerContext`. Swallow a corrupt file (`catch (JsonException)`) → empty state rather than crash.

### Pattern: client backend (`KHost.Mobile.Client`)
- Stays **MAUI-free with zero package references** (pure BCL). One feature per folder/namespace.
- `HttpClient` is **injected** via primary constructor (never `new`); base address/headers are configured at DI registration, not in the library. Registered as a typed client (`AddHttpClient<IFace, Impl>`).
- Isolate parsing in a `static` "pure — no network" parser class; the service does HTTP + error mapping only.
- One `sealed` exception per feature: `sealed class FooException(string message, Exception? inner = null) : Exception(message, inner)`, with messages written to be shown in the UI. Deserialize via manual `JsonDocument` traversal (no reflection serializer here — that's the host's convention, not the client's).

### Pattern: Blazor component
- Single-file `.razor` (no code-behind, no scoped `.razor.css`; all CSS in `wwwroot/app.css`). Keep components single-purpose. `@inject` (never `[Inject]`); injected services get short semantic field names (`Store`, `Settings`, `JS`). `[Parameter]` props get `<summary>` docs.
- Load data in `OnInitializedAsync`, subscribe to store `Changed`, and implement `IDisposable` to unsubscribe. `async Task` handlers — never `async void`; fire-and-forget is an explicit `_ = FooAsync()` with an internal try/catch. `InvokeAsync(StateHasChanged)` from async continuations; bare `StateHasChanged()` from sync / `[JSInvokable]` paths.
- JS interop only in `OnAfterRenderAsync`: one `wwwroot/js/<feature>.js` per feature exposing `window.kh<Feature>.register(...)`, bound once via a `_xBound` flag; C#↔JS round-trips use `DotNetObjectReference` + `[JSInvokable]`.
- CSS: `--kh-` design tokens in `:root`; light/dark via `@media (prefers-color-scheme)` plus a `[data-theme]` override; BEM class naming (`block__element`, `block--modifier`, `is-`/`active` state).

### Housekeeping
- **Do NOT commit or push unless explicitly asked.**
- **`/research/` is gitignored and must never be committed** — it holds local planning/research notes and scratch data. Don't stage it, don't offer to commit it, and don't propose removing it from `.gitignore`.
- Secrets via user-secrets/config — never hard-coded or committed.

## Roadmap (server integration — deferred)

Eventual slice: **join venue → search library → request song → live queue** against KHost.Online. When it lands, `KHost.Mobile.Client` gains the typed HTTP + `HubConnection` server client and re-takes a `KHost.Contracts` reference (removed for now so this repo builds standalone). `KHost.Online`'s REST slice is scaffolded and runtime-verified in its own repo.

Server repo: `../KHost.Online` (see its own `CLAUDE.md`). Planned first-slice REST + SignalR surface, all route strings in `KHost.Contracts/Routes.cs`:

| Method | Route (`Routes.Api.*`) | Purpose |
|---|---|---|
| POST | `/api/venues/join` | join code + display name → session token (`Bearer`) |
| GET | `/api/songs/search?q=` | filtered library (auth required) |
| POST | `/api/queue/request` | add self to queue for a song; broadcasts `QueueUpdated` |
| GET | `/api/queue` | current queue snapshot |
| Hub | `/hubs/queue` | SignalR push — connect with `?access_token=<sessionToken>` |

Planned auth (first slice): an opaque server-side session token sent as `Authorization: Bearer <token>` — a deliberate stand-in for a signed JWT later. Demo venue join code is `DEMO`. `IQueueClient` push methods: `QueueUpdated(queue)`, `NowPlayingChanged(nowPlaying?)`, `YoureUpNext()`.
