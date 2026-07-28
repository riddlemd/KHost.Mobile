# 🧑‍💻 Development & design notes

Developer-facing docs for **KHost Cue** — the reasoning behind a few non-obvious parts of the app, plus genuine first-time build setup. For the product overview, features, and screenshots, see **[README.md](README.md)**. For coding conventions, tech stack, and project layout, see **[AGENTS.md](AGENTS.md)**. For the full build/deploy/test command reference, see the wiki's [Building and Testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing) page.

## 🚀 Building from source

### Prerequisites

- **.NET 10 SDK** with the MAUI workload:
  ```bash
  dotnet workload install maui
  ```
- **Android**: the Android SDK, a JDK 17+, and an emulator or a connected device. If you have neither SDK nor JDK, .NET Android can fetch both at the versions this project targets:
  ```bash
  dotnet build src/KHost.Mobile.UI/KHost.Mobile.UI.csproj -f net10.0-android -t:InstallAndroidDependencies \
    -p:AndroidSdkDirectory=$HOME/Library/Android/sdk -p:JavaSdkDirectory=$HOME/Library/Android/jdk \
    -p:AcceptAndroidSDKLicenses=true
  ```
  Then export `ANDROID_HOME` and `JAVA_HOME` at those paths so plain `dotnet build` finds them — otherwise every build needs the `-p:AndroidSdkDirectory=… -p:JavaSdkDirectory=…` flags. (The warnings that target logs on its *first* run are from the evaluation pass before the SDK exists; they clear once it's installed.)
- **iOS**: a paired Mac (iOS cannot be built on Windows).
- **macOS (Mac Catalyst)**: full **Xcode** — Command Line Tools alone is not enough. Point the toolchain at it with `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`.

> Restore walks **every** target framework the project declares, even when you build a single head with `-f`, so a build fails until all of them have workloads. `dotnet workload restore src/KHost.Mobile.UI/KHost.Mobile.UI.csproj` installs exactly the set this project needs.

### Build & run

Full command reference — all four heads, deploy-and-launch, the Windows/Catalyst iteration loops — lives on the wiki's [Building and Testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing) page. AGENTS.md carries the two rules that matter for a device build: always deploy the Debug build with `-t:Run`, never `adb install` the APK (Fast Deployment keeps the .NET assemblies out of the APK, so a bare install crashes on launch), and the `<MauiVersion>` pin (currently `10.0.80`) — don't "clean it up" back to the workload default, which crashes on Android 16 launch.

### Backing up on-device data (before a risky redeploy)

Deploying with `-t:Run` **updates the app in place** — the on-device data (`files/*.json`, `shared_prefs/`) survives. It's only wiped by an **uninstall**, and there are three sneaky ways that happens without you asking for it: deploying a build signed with a **different debug keystore** (fresh machine, regenerated `~/.android/debug.keystore`) fails to install over the existing app with a signature mismatch, so the tooling falls back to uninstall + reinstall; a **package-id change** makes Android treat the build as a different app entirely; and a manual "uninstall to fix a launch crash" does the same on purpose. All three take your singers, song lists, tonight sets, venues and settings with them.

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

> **Emulator tip — Fast Deployment ships assemblies, not `wwwroot`.** The Razor/JS/CSS assets live in the APK, so if the APK is stale while `-t:Run` pushes current assemblies, C# calls JS that isn't there (`Could not find 'khArtVisibility.register'`) and the circuit dies with "An unhandled error has occurred". Easy to hit on an emulator started with `-no-snapshot-save`: a restart reverts `/data` (APK included) while the next `-t:Run` is a no-op that only restores assemblies. `adb uninstall khost.mobile` then redeploy. Note an uninstall re-runs the **first-run tutorial**, whose full-screen `.tutorial__catch` swallows every tap — flip `settings.tutorial_completed` in `shared_prefs` before driving the UI.

### Clearing the scratch folders

Both gitignored scratch folders grow without bound — a backup is ~48 MB per run, and the UI walkers drop a PNG per screenshot — so a day of on-device work can leave over a gigabyte behind:

```bash
dotnet run scripts/cleanup-scratch.cs --                    # keep the 3 newest backups, clear all shots
dotnet run scripts/cleanup-scratch.cs -- --dry-run          # show what would go, delete nothing
dotnet run scripts/cleanup-scratch.cs -- --keep 5           # keep the 5 newest backups
#   --backups-only / --shots-only   limit it to one folder
```

Backups are the only safety net against a redeploy that reinstalls, so the newest `--keep` are always retained (`--keep 0` empties the folder and warns). Screenshots are regenerated by the walkers, so all of them go. Only the script's own artifacts are touched — a `khost-mobile-*.tar.gz` and image files respectively — so anything else parked in either folder survives.

### Sample data for testing

See AGENTS.md → Gotchas for a public YouTube Music playlist link that imports cleanly via Import & Export → YouTube Music — handy for populating the list while testing.

## 🧪 Testing

Two xUnit projects, split by what they touch. Commands, and the rule that both must pass before a commit, are in AGENTS.md's Housekeeping section; the wiki's [Building and Testing](https://github.com/riddlemd/KHost.Mobile/wiki/Building-and-Testing) page has the full reference.

Neither test project needs the MAUI workload: they target plain `net10.0` and reference `KHost.Mobile.Abstractions`, `.Common`, `.Infrastructure` and `.Clients` — none of which knows MAUI exists. The stores' only device dependency — the app-data folder — is abstracted behind `IAppDataDirectory`, which the integration tests point at a throwaway temp directory.

> This used to require ~56 hand-maintained `<Compile Include>` links, one per file, because the models and stores lived in the MAUI head and a `net10.0` project can't reference a MAUI project. Pulling them out into Abstractions and Infrastructure is what removed that list. **If you find yourself adding a `<Compile Include>` to a test project, the type is in the wrong project** — move it down a layer instead.

### Driving the running UI

Gesture- and sheet-shaped changes are verified by driving the app's WebView over CDP rather than through either test suite — see AGENTS.md → Commands → UI automation for the attach flow and the on-device gotchas, and [`playwright/README.md`](playwright/README.md) for the full how-to. `walk_tutorial.mjs` (in both `playwright/device/` and `playwright/emulator/`) is the worked example to start from.

## 📸 Screenshots

**The README grid is captured from a physical device** by `playwright/device/shoot_docs.mjs` (plus `shoot_spelling.mjs`, which needs a song with a pending suggestion and so adds and removes a misspelled one). Attach as `playwright/README.md` describes, then `node device/shoot_docs.mjs [name …]`. Shots land at the device's native size — **1080 × 2400** from a Pixel 8, Android status and nav bars included — so re-shoot the whole set from one device rather than mixing sources.

Capture goes through `adb exec-out screencap`, **not** `page.screenshot`: CDP capture hangs on this WebView, on a current device head as well as the emulator's older one. Only navigation runs over CDP. Two traps the script encodes, both of which silently photograph the wrong screen: sheets **stack** (detail → history), so close the *last* `.sheet__close`, not the first; and the venue/singer chip popovers are **not** sheets, so they survive a sheet close and then eat the next click. Every shot asserts what's on screen before the shutter, because a wrong-screen capture is otherwise invisible until you look at the PNG.

**Shoot the grid in dark mode, with the active singer on the brand violet.** Both are set on the device before you start: dark via the header **⋮ → 🌙 Dark mode** (it writes `data-theme` on `<html>`, overriding the OS preference, and persists), violet by making a singer whose colour is `SingerColors.Default` (`#7c3aed`) the active one. This isn't taste — the active singer's colour overrides the `--kh-primary` tokens across the whole chrome, so shooting while a teal or amber singer is active produces a grid that doesn't match the app's own branding, and a set shot across two singers won't even match itself. Check the header chip before the first shutter.

**Mobile-preview target size:** **786 × 1704 px** — a **393 × 852** (iPhone 15/16) viewport at **2× device-pixel-ratio**. This is what `App.PinToMobilePreviewViewport` pins the Catalyst window to; the docs grid no longer uses it.

## 🎨 Design notes

**Album art — why `blob:` URLs and not `<img src>` / `file://`.** Covers are cached as plain image files in the app's private data directory (`Data/album-art/`, named by a hash of the source URL). But the Blazor WebView serves only the bundled, read-only `wwwroot`, and its page origin is `https://0.0.0.1` — so it has **no route to a file in the data directory**: `file://` access to the app-private dir is sandbox-blocked, an `https` page loading a `file://` resource is a cross-origin/mixed-content violation, and `wwwroot` can't be written to at runtime.

Referencing the cached file directly would therefore need a **per-platform serving handler** — WebView2 `SetVirtualHostNameToFolderMapping`, Android `WebViewAssetLoader` / `shouldInterceptRequest`, iOS `WKURLSchemeHandler` — three separate native implementations, the riskiest of which (Android) can't be verified without an on-device run.

Instead, the cover bytes are streamed to the WebView via a `DotNetStreamReference` and turned into a `blob:` object URL in `wwwroot/js/album-art.js` — **one implementation that behaves identically on every platform**, so it's verifiable once. The card's CSS background then holds a short `blob:` URL rather than a base64 `data:` copy of every image. `js/album-art.js` owns the object-URL lifecycle (revoked when a cover is replaced, on a singer switch, and on page teardown). The platform-serving approach remains a valid alternative if the C#↔JS transfer ever becomes a bottleneck.

**A cached cover is written atomically, and it matters more than for the JSON stores.** The cache decides "do I already have this?" with `File.Exists`, so a write torn by an app kill would leave a truncated image that counts as a hit **forever** — the card renders broken and never re-downloads, because nothing ever re-checks a file that exists. Writing through `AtomicFile` (a `.tmp` sibling, then a rename) means a failed write never produces a target file at all, and the next request simply fetches it again. The read path's 0-byte check predates this and stays: it still covers images written before the fix. The JSON stores get the same treatment for a milder reason — there a corrupt file is at least *detected* on parse and quarantined.

**Asking for a cover is what fetches it.** `IAlbumArtService.UriFor(song)` returns the `blob:` URL if it's ready and otherwise *starts the work to get it* — discovering the artwork URL (iTunes, then Deezer when iTunes has no cover), downloading, caching, handing it to the WebView — then raises `Changed` so the surface repaints. The tempting alternative — callers pre-declaring what to load (`LoadAsync(theseSongs)`) — puts the burden in the wrong place: every surface has to remember to do it, and any surface showing a song from *outside* the set it declared (a 🎲 pick, a detail sheet) silently renders a blank card or grows a bespoke workaround. With requests driving the fetching, callers only render what they're given, so **a surface that displays cover art needs no art code at all** — but it does need to subscribe to `Changed`, because covers land after the render that asked for them and the art is a Blazor-rendered inline style.

Three things about it are counter-intuitive enough to be worth stating, all of them learned by measuring on a real device:

- **Visible, not rendered, is what drives fetching.** `wwwroot/js/art-visibility.js` runs one `IntersectionObserver` over every element carrying `data-art-song`, and only songs it reports on screen are fetched. This matters because My Songs keeps every card you've scrolled past in the DOM, so after a long scroll the rendered set is many times the visible one. Don't be tempted to key a cap off *rendered* instead — that thrashes endlessly, evicting a cover the next render asks straight back. Nothing currently visible is ever evicted, which is precisely what makes the cap safe; off-screen covers get `OffScreenCovers` worth of headroom for scrolling back, then the longest-gone are dropped. A surface joins in simply by carrying the attribute — that's how the detail and roll sheets get covers for songs the list never showed.
- **The loading placeholder means "an image is coming", not "we're looking".** `IsFetching` is false during discovery, when it isn't yet known whether the song has a cover at all — and since plenty of songs turn out to have none, a placeholder there would flash across much of the list and then vanish. It only appears once the artwork URL is known, and its shimmer + angled "Loading cover" watermark live in `::before`/`::after` under the card's content, so nothing shifts when the real cover lands. Tonight's rows get the shimmer without the watermark: a single compact line has no free corner for it.
- **Discovery is paced, downloads aren't.** Because scrolling asks about every song it renders, and a good share of them have no cover to find, an unpaced sweep fires hundreds of back-to-back iTunes calls and earns a rate-limit block. Each lookup that actually goes to the network is followed by a short pause; fetching an already-known cover URL hits the artwork CDN instead and runs at full speed.

**What a repaint is allowed to cost.** A cover landing repaints the whole page, and a cold library lands hundreds of them, so anything O(library) in the render path gets multiplied by every cover. Four things keep that flat, and all four are easy to undo by accident:

- **The filter+sort result is memoized** on the filter/sort signature plus the `_items` reference. `Sorted()` runs two `OrderBy`s over the library; re-running it per repaint was the single largest render cost.
- **`UsedTags` is memoized too** — it's a parameter of the always-rendered detail sheet, so as a plain property it walked every song's tags on every repaint whether or not any tag UI was open. Same trap for anything else wired into an always-present component.
- **`AlbumArt.ObserveAsync()` is gated** on a signature of what can carry `data-art-song` (rendered count, list identity, the open detail/roll song). It scans the whole DOM, and My Songs keeps every scrolled-past card, so calling it unconditionally from `OnAfterRenderAsync` re-scanned hundreds of nodes per repaint. The signature must include any surface that swaps the song under a live element — that's how the roll sheet's reroll still gets its cover.
- **`AlbumArtService.Changed` is coalesced** (trailing ~50 ms) and discovery results are **persisted in batches**, not per song. Each store write rewrites the whole song-list file and fires `Changed`, so per-song writes made a cold sweep O(library²) in disk I/O. Measured over a deep scroll of a 533-song library: 17 discoveries → **1** file write.

**Spelling suggestions — one anchored field, two edits.** Matching alone can't tell a misspelled song from one that isn't in the catalogue: `ITunesResponseParser` demands an exact normalized match on *both* title and artist, so "Bohemian Rapsody" matches nothing — no year, no genre, no cover, no reason given. So alongside the match it keeps the closest near-miss as a `TrackLookupResult.Suggestion`. Both surfaces mark it with a ⚠ beside the song's name — the name is what's wrong, so that's where the affordance belongs — and in the detail sheet that ⚠ is a button that unfolds the *"Did you mean …?"* offer. It stays folded by default: the suggestion is a guess, and an unprompted panel would push the song's own details down the sheet.

The threshold is the whole design, because a false suggestion on a correctly-spelled song is worse than no suggestion. Three rules do the work, and the first matters most: **one field has to be spelled exactly right**, and only the other may differ. A typo is a slip in one field; demanding an anchor is what stops an unrelated song — or, more dangerously, a cover by a different artist — from being proposed as a fix. On the live response for "Radiohead Creap", three of six results are titled exactly "Creep" by *other* artists; the anchor rejects all of them (fixture-pinned in `ITunesResponseParserSuggestionTests`). The other two rules are a ceiling of **2 edits** and a floor of **5 characters per edit**, so "Yes"/"Yet" and "Africa"/"America" stay separate songs while "Helna"/"Helena" doesn't.

**Why iTunes is asked first, and what Deezer adds.** We aren't spell-checking — we're reading a catalogue's own correction out of its ranked results, so the question is which catalogue to trust about how songs are spelled. That rules out crowd-edited sources: MusicBrainz contains "Bohemian Rapsody" as a real recording title and its fuzzy search ranks it *above* the correct spelling (score 100 vs 92), so asking it to check your spelling confirms the typo. iTunes is label-supplied and curated, which is exactly what makes it an authority — and its suggestion is free, riding a call we already make for year/genre/art.

Deezer is a fallback for catalogue gaps only, consulted when iTunes returned neither a match nor a near-miss. It has the same dirty-data hazard (its top hit for "Jacques Brel Ne Me Quite Pas" is titled with the misspelling, while iTunes returns "Ne me quitte pas"), which is the second reason for the ordering. Measured against the live APIs, iTunes handles the overwhelming majority — typo'd deep cuts included — so treat Deezer as cheap insurance, not a workhorse. It needs its **plain free-text** query: the field-scoped `artist:"…" track:"…"` form the cover-art path uses is exact-only and returns *zero* results for a typo, which is why this is a separate call rather than a second reading of the art response. A general-purpose spell checker (Hunspell, SymSpell) is the wrong tool entirely here — its dictionary is words in a language, and band names are precisely what such dictionaries exclude, so it would "correct" Chvrches → Churches and Ke$ha → Kesha.

Two smaller calls worth keeping: the suggestion is offered with bracketed asides stripped (`TrackTextNormalizer.StripAsides`), because a typo search surfaces "Creep (Acoustic)" long before "Creep" and writing that version suffix into the user's own title isn't the correction they asked for; and **nothing is filled from a near-miss** — it hasn't been confirmed as the right song, so accepting the correction is what re-runs the lookup and fills year/genre/cover from the now-exact match. Both answers are terminal: `MetadataLookedUp` is already set, so a dismissed suggestion can never be raised again by a later lookup.

**Crash-safe store writes.** Every JSON store writes to a same-directory `.tmp` file and atomically renames it over the target (`AtomicFile.WriteAsync`) — a same-volume rename, so a write interrupted by an app kill or power loss leaves the *last good* file intact instead of a truncated one. (The load path treats a corrupt file as "start empty", which for a direct overwrite would silently lose the whole list.) A file that fails to parse on load is moved aside to a `.corrupt` sibling rather than being overwritten by the next save, so the bad bytes are preserved for recovery.

**Split button — one control, a default action plus a menu.** `SplitButton` / `SplitButtonItem` (`Components/`) render a primary action segment beside a chevron that drops a menu of related actions — e.g. *Log performance* with alternate ways to log it, or *Find on YouTube* with Spotify / KaraFun / Lyrics behind the chevron to reclaim sheet height. Reusable: pass the default via `OnPrimary` and the extras as `SplitButtonItem` children (each takes `Icon` / `Description` / `Separated`), with `Direction` (Down/Up, for buttons low in a sheet), `Align`, and `Variant` (Primary/Tonal/Secondary) knobs. Dismissal reuses the header ⋮ menu's approach — a transparent full-screen scrim for an outside tap, plus `IBackButtonService` so the Android back button closes the menu instead of navigating — so there's **no bespoke JS**; it's styled with the shared `.btn` variants and `--kh-` tokens.

**Surprise me — tap to roll, hold for options.** The 🎲 is a second FAB above the "+", deliberately 75% its size, offset up and to its right, and tonal rather than solid: three signals that it's the secondary action on the screen. A tap rolls immediately and shows the pick in `RollResultSheet` rather than opening the song outright — rerolling is the common case, and going through the detail sheet each time made it a three-tap loop. A press-and-hold opens the options. Since no assistive-tech gesture maps to a long press, the result sheet's *Options* action is the reachable equivalent — the same pattern Venues and Singers use for press-and-hold.

The result is rendered as the app's own `.song-card` (same classes, same album-art treatment, just taller) so a suggestion looks like it was lifted out of the list rather than announced by a system message. A snackbar is the obvious choice here and is wrong twice over: it borrows the undo toast's deliberately-dark pill, which ignores the theme tokens, and it offers no dismissal that isn't a navigation. Being a `Sheet` avoids both — the backdrop, ✕, pull-down-to-dismiss and scroll lock all come for free. *Add to tonight* is the primary button, not *Open*: lining a song up is what a singer usually wants from a suggestion, and reading its details is the follow-up.

One trap worth knowing: a roll can land on a song the list never rendered, so the sheet can't count on the cover being cached. It carries `data-art-song` like every other art surface, and asking for the cover is what fetches it (see *Asking for a cover is what fetches it* above) — no bespoke pre-loading. A pick with no cover is a normal outcome rather than a failure, and the hero height is scoped to `.song-card--art` so a coverless pick doesn't render a dead gap.

The draw rules live in `Infrastructure/Logic/SurprisePicker.cs`, pure and deterministic given a roll value, so the narrowing and the star weighting are unit-tested without a UI or an RNG. Two things worth keeping: a restriction that would empty the pool is **skipped rather than honoured** (a one-tap picker has nowhere to explain "no matches"), and every song keeps a small weight floor so a run of bad nights can't make one undrawable. `press-hold.js` owns the pointer sequence rather than using `@onclick` plus a timer, because the WebView fires its own long-press callout early and still delivers a click afterwards — which would run the hold *and* then roll.

**Year picker — a grid of the library's own years.** `YearPickerSheet` (`Components/`) sets either end of the My Songs year filter. Three decisions worth keeping: it offers **only years actually present in the library**, not every year in the `min`–`max` span, so a pick can never produce an empty result set (the filter's `_libraryYears` comes off the same pass that computes the bounds); it lays them out as a **grid rather than a list**, because a span like 1959–2024 is 65 rows to scroll but only 13 as a five-column grid — the same reason the platform date pickers show years in a grid; and years that would put the two handles the wrong way round render **disabled rather than being silently clamped**, so the year you tap is the year you get. The dual-handle slider is unchanged and still drives the same two values.

**Mac Catalyst head — a layout preview, not a product.** There is no desktop KHost Cue and none is planned. The Catalyst head exists for the same reason as the Windows head: iterating on the Blazor UI without waiting on an emulator. Everything about it is tuned for that and nothing for shipping — it builds `maccatalyst-arm64` only (native on Apple silicon, one slice instead of two), and `App.CreateWindow` opens it at exactly the **393 × 852** mobile-preview viewport documented under Screenshots, so what you see matches the screenshot grid. It stays resizable so you can drag it wider to find where a layout breaks.

Landing that exact viewport takes several Catalyst workarounds (a min = max size-restriction pin, a measured title-bar correction, and the Mac idiom in `UIDeviceFamily` for 1 : 1 point scaling) — the mechanics and their reasons are commented where they live, in `App.PinToMobilePreviewViewport` and the Catalyst `Info.plist`. To capture a screenshot from this head, grab the window (`screencapture -l <windowid>`) and crop the title bar off the top — the remainder is exactly 786 × 1704.

Treat a wide Catalyst window as a diagnostic, not a bug: the shell is deliberately mobile-first (fixed bottom tab bar, full-bleed cards, swipe and press-and-hold gestures), so stretching it *should* look wrong. Don't add desktop breakpoints to `wwwroot/app.css` to "fix" it.

Mouse input covers the gestures: `swipe.js` runs off pointer events, so click-and-drag is a swipe and click-and-hold is a press-and-hold. Where a gesture is awkward to trigger, the reachable equivalents added for assistive tech work too — Venues' *Active* toggle and the singer sheet's *Switch to this singer*.

**Venue catalog QR — generated on-device, and why error correction is Medium.** A venue's KaraFun songbook can be
shown as a QR code (Venues → the catalog button's chevron → *Show QR Code*) so someone else can scan it off the
screen. Generation is local: the app is offline-only, and calling a QR-image web service would both break that
promise and put a venue's ID in someone else's server log. The encoder is `Net.Codecrete.QrCodeGenerator` (MIT,
zero dependencies, trimming-enabled) behind `IQrCodeService`, so the package is named in exactly one class.

Three choices worth knowing before changing it:

- **SVG, not a bitmap.** A `viewBox` measured in modules scales to any size with CSS, needs no imaging stack
  (`System.Drawing` is Windows-only since .NET 6 and would have ruled the library out), and needs no cache.
- **Error correction `Medium`, not `Quartile`/`High`.** The higher levels exist to survive *physical* damage —
  dirt, scratches, print smudging — none of which a screen suffers. They pay for it with denser modules, which is
  what actually costs a scan at arm's length. Raise it only if a logo is ever overlaid on the code, since an
  overlay masks real modules. For a ~31-character catalog URL, Medium and Quartile both land on version 3
  (29 modules) anyway; Medium just leaves more headroom before the next version bump.
- **Fixed light colors in both themes.** The sheet forces a white quiet zone and dark modules rather than
  inheriting `--kh-` tokens — an inverted or tinted code is where phone scanners start failing.

Screen brightness is deliberately **not** boosted while the code is shown: MAUI has no cross-platform brightness
API, so it would mean per-platform code for a problem a normally-lit screen doesn't have.

## 📁 Project structure

AGENTS.md's Solution / project layout table covers the five shipping projects — `KHost.Mobile.Abstractions`, `.Common`, `.Infrastructure`, `.Clients` and `.UI` — and the layering rule between them, in more detail. The two test projects aren't in that table:

| Project | Role |
|---|---|
| `KHost.Mobile.UnitTests` | xUnit unit tests for the pure, no-I/O logic (parsers, `Genres`, `SongListItem`). |
| `KHost.Mobile.IntegrationTests` | xUnit integration tests for the JSON stores against a real temp folder, via a fake `IAppDataDirectory`. |
