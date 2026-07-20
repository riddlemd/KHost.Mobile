# UI automation (Playwright)

Scripts for driving the **KHost Cue** Blazor WebView from a dev machine — walking the first-run
tutorial, exercising a flow, capturing screenshots. Debug builds enable WebView debugging
(`AddBlazorWebViewDeveloperTools`), so we attach over the Chrome DevTools Protocol.

## Why this shape

Playwright's native Android API (`_android.devices().webViews()`) is **not** used: it returns nothing
unless you let it install a companion APK on the device, and the Python binding doesn't ship the
Android surface at all. Instead we do the `adb forward` ourselves and attach with
`chromium.connectOverCDP(...)`. That still gives a full Playwright `Page` (auto-waiting locators,
screenshots) while touching nothing on the device.

## Setup

```bash
cd playwright
npm install            # pulls playwright-core (pinned in package-lock.json). No browser download needed.
```

Requires Node 20+ and the Android platform tools (`adb`) on `PATH`.

## Attach to a device (Android)

```bash
# 1. App must be running and foregrounded, screen awake:
adb shell input keyevent KEYCODE_WAKEUP
adb shell monkey -p khost.mobile -c android.intent.category.LAUNCHER 1

# 2. Forward the WebView's devtools socket to tcp:9333:
PID=$(adb shell pidof khost.mobile)
adb forward tcp:9333 localabstract:webview_devtools_remote_$PID
```

Override the port with `CDP_PORT`, and target a specific device for screenshots with `KH_SERIAL`.
The Windows head works too — launch it with `--remote-debugging-port=9333` and skip the `adb` steps.

## Run

```bash
npm run walk-tutorial      # drives Settings → Help → Replay, then steps every tour card,
                           # logging chapter/title/route/spotlight and saving shots/ screenshots.
```

## Write your own

```js
import { attach, menuTo, shot, TAP } from './khdrive.mjs';

const { browser, page } = await attach();   // full Playwright Page
await menuTo(page, 'Venues');                // open header ⋮ menu → item
await page.getByText('Add').click(TAP);      // TAP = { force: true }; see below
shot(page, 'venues-add');                    // screenshot to shots/
await browser.close();                       // detaches CDP; does NOT kill the app
```

### Gotchas (learned on-device)

- **Use `TAP` (`{ force: true }`) on clicks.** The app keeps subtle chrome transitions running, so
  Playwright's "stable" actionability check times out on the Android WebView even for tappable controls.
- **Screenshots go through `adb exec-out screencap`, not `page.screenshot`** — CDP capture hangs on
  this WebView. The app's WebView is full-screen, so the device frame is the page. (`shot()` handles this.)
- **Navigate by clicking the app's own links** (`.header-menu__btn` → item, `.page-back`, NavLinks) —
  never `location.assign` / `document.write`, which break the Blazor circuit.
- **Collapsible sections keep their open/closed state**, so check-then-open rather than blind-toggle.
- **`browser.close()` only detaches CDP** — it leaves the app running.

`shots/` and `node_modules/` are gitignored.
