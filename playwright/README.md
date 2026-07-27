# UI automation

Scripts for driving the **KHost Cue** Blazor WebView from a dev machine — walking the first-run
tutorial, exercising a flow, capturing screenshots. Debug builds enable WebView debugging
(`AddBlazorWebViewDeveloperTools`), so we attach over the Chrome DevTools Protocol.

## Layout — pick by target, not by tool

```
device/     khdrive.mjs        Playwright driver  → a physical device (and the Windows head)
            walk_tutorial.mjs
emulator/   cdp.mjs            raw-CDP driver     → the Android emulator
            walk_tutorial.mjs
shots/      screenshots from either walker (gitignored)
```

The two `walk_tutorial.mjs` walkers are the canonical examples, not the whole inventory — a
rotating set of one-off exercise/screenshot scripts (`shoot_docs.mjs`, `shoot_spelling.mjs`,
`typo_*.mjs`, …) lives beside each driver, all following the same attach pattern.

**Why two.** Playwright can't attach to the emulator's WebView: `connectOverCDP` calls
`Browser.setDownloadBehavior` on connect, which that older WebView doesn't implement — it fails with
*"Browser context management is not supported"* before it ever reaches the page. A physical device's
WebView is current and works fine. So `emulator/cdp.mjs` is a small raw-CDP client (Node's built-in
`WebSocket`, no dependency) covering the same ground.

Playwright's native Android API (`_android.devices().webViews()`) is **not** used either: it returns
nothing unless you let it install a companion APK on the device.

## Setup

```bash
cd playwright
npm install            # pulls playwright-core (pinned in package-lock.json). No browser download needed.
```

Requires Node 20+ and the Android platform tools (`adb`) on `PATH`. The emulator scripts need no
`npm install` — they only use built-ins.

## Attach

```bash
# 1. App running and foregrounded, screen awake:
adb shell input keyevent KEYCODE_WAKEUP
adb shell monkey -p khost.mobile -c android.intent.category.LAUNCHER 1

# 2. Forward the WebView's devtools socket to tcp:9333:
PID=$(adb shell pidof khost.mobile)
adb forward tcp:9333 localabstract:webview_devtools_remote_$PID
```

Override the port with `CDP_PORT`, and target a specific device with `ANDROID_SERIAL` (or `KH_SERIAL`,
which wins if both are set; the emulator scripts default to `emulator-5554`). With exactly one transport
attached you can omit it — with more, `shot()` fails listing the candidates and a ready-to-paste command.
Setting `ANDROID_SERIAL` also covers any `adb` your own script shells out to, since adb honours it
natively. The Windows head works too — launch it with `--remote-debugging-port=9333` and skip the `adb`
steps.

## Run

```bash
npm run walk-tutorial            # device/   — drives Settings → Help → Replay, then steps every card
npm run walk-tutorial:emulator   # emulator/ — same walk; re-arm the tour first (see below)
```

Both log chapter/title/route/spotlight per card and save screenshots to `shots/`.

The emulator walker can't use Settings → Help → Replay, because the app has to restart to pick the
flag up. Flip it directly, then relaunch:

```bash
# Pull, edit, push back — an in-place `run-as … sed -i` on the device mangles the quotes in the pattern.
adb -s emulator-5554 shell am force-stop khost.mobile
adb -s emulator-5554 shell run-as khost.mobile cat shared_prefs/khost.mobile_preferences.xml > /tmp/kh-prefs.xml
sed -i '' 's/"settings.tutorial_completed" value="true"/"settings.tutorial_completed" value="false"/' /tmp/kh-prefs.xml   # GNU sed: drop the ''
cat /tmp/kh-prefs.xml | adb -s emulator-5554 shell "run-as khost.mobile sh -c 'cat > shared_prefs/khost.mobile_preferences.xml'"
dotnet build KHost.Mobile.UI/KHost.Mobile.UI.csproj -f net10.0-android -t:Run "-p:AdbTarget=-s emulator-5554"
```

## Write your own

```js
// device/
import { attach, menuTo, shot, TAP } from './khdrive.mjs';

const { browser, page } = await attach();   // full Playwright Page
await menuTo(page, 'Venues');                // open header ⋮ menu → item
await page.getByText('Add').click(TAP);      // TAP = { force: true }; see below
shot(page, 'venues-add');                    // screenshot to ../shots/
await browser.close();                       // detaches CDP; does NOT kill the app
```

```js
// emulator/
import { evaluate, tap, swipeDown, close } from './cdp.mjs';

await tap('.song-card');                     // opens the detail card
await swipeDown('.sheet');                   // pull-to-dismiss it
console.log(await evaluate(`return document.body.classList.contains('kh-sheet-open')`));
close();
```

### Gotchas (learned on-device)

- **Never use `adb shell monkey` to launch or foreground the app — it changes the phone's settings.**
  It reads like a convenient launcher (`monkey -p khost.mobile -c android.intent.category.LAUNCHER 1`)
  but it is a random *input fuzzer*: its event mix includes rotation events, and delivering one can
  switch the device's **auto-rotate** preference on. On someone's real phone that is theirs to set, not
  ours to change. Call `foreground()` from `khdrive.mjs` instead — it resolves the launchable activity
  from the device (the MAUI `crc64….MainActivity` name is a hash that moves when a namespace does) and
  runs `am start`, which touches nothing else. You need this whenever Android suspends a backgrounded
  WebView and `attach()` starts timing out on the devtools port.
- **`tap`/`swipeDown` over a DOM `.click()`** (emulator) — they dispatch real
  `Input.dispatchTouchEvent` touch points, so gestures wired through `swipe.js` (tap vs. hold vs.
  swipe) and `khSheet` (pull-down-to-dismiss) actually fire; a `.click()` reaches Blazor's `@onclick`
  and nothing else.
- **Use `TAP` (`{ force: true }`) on clicks** (device) — the app keeps subtle chrome transitions
  running, so Playwright's "stable" actionability check times out even for tappable controls.
- **Screenshots go through `adb exec-out screencap`, not `page.screenshot`** — CDP capture hangs on
  this WebView. The app's WebView is full-screen, so the device frame is the page.
- **Navigate by clicking the app's own links** (`.header-menu__btn` → item, `.page-back`, NavLinks) —
  never `location.assign` / `document.write`, which break the Blazor circuit.
- **Collapsible sections keep their open/closed state**, so check-then-open rather than blind-toggle.
- **Seeding an emulator:** write the store files directly (`adb shell run-as khost.mobile sh -c 'cat >
  files/…'`) rather than driving the add form. The per-singer filenames use the **dash-less** GUID
  (`song-list-<32 hex>.json`) — a dashed name is silently ignored, since nothing reads it.

`shots/` and `node_modules/` are gitignored.
