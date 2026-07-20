// Reusable driver for the KHost Cue WebView (Android device head, or the Windows head's WebView2).
//
// WHY this shape: Playwright's native `_android` webview discovery returns nothing unless its
// companion-APK driver is installed on the device — which we won't do to a physical phone. So we
// attach with `connectOverCDP` over a devtools port WE forward with adb. That still yields a full
// Playwright `Page` (auto-waiting locators, screenshots) — the whole reason to use Playwright over a
// hand-rolled `Runtime.evaluate` loop — while touching nothing on the device.
//
// One-time device setup (Android):
//   PID=$(adb shell pidof khost.mobile)
//   adb forward tcp:9333 localabstract:webview_devtools_remote_$PID
// then: import { attach } from './khdrive.mjs'
//
// Navigation rule (see the cdp-navigate-via-links note): reach pages by clicking the app's own
// links (.header-menu__btn → item, .page-back, NavLinks) — never location.assign / document.write,
// which break the Blazor circuit.
import { chromium } from 'playwright-core';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

/** Connect to the forwarded devtools port and return { browser, page }. `page` is the app's WebView. */
export async function attach(port = process.env.CDP_PORT || 9333) {
    const browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
    const ctx = browser.contexts()[0];
    const page = ctx.pages()[0] ?? (await ctx.waitForEvent('page'));
    return { browser, page };
}

// The app's CSS keeps subtle transitions running on chrome, so Playwright's "stable" actionability
// check can time out on the Android WebView even when a control is plainly tappable. `force:true`
// skips that check — safe here because we always target a specific, known-visible control.
export const TAP = { force: true };

/** Open the header ⋮ menu and click the item whose text contains `label` (case-insensitive). */
export async function menuTo(page, label) {
    await page.click('.header-menu__btn', TAP);
    await page.getByRole('menuitem', { name: new RegExp(label, 'i') })
        .or(page.locator('.header-menu__item', { hasText: new RegExp(label, 'i') }))
        .first().click(TAP);
}

// Save a screenshot under playwright/shots/. Uses `adb exec-out screencap` rather than Playwright's
// page.screenshot — CDP capture hangs on the Android WebView ("fonts loaded" then never returns), and
// the app's WebView is full-screen so the device frame IS the page. `page` is accepted for a uniform
// signature but unused. Set KH_SERIAL to target a specific device.
export function shot(_page, name) {
    const out = fileURLToPath(new URL(`./shots/${name}.png`, import.meta.url));
    const serial = process.env.KH_SERIAL;
    const args = [...(serial ? ['-s', serial] : []), 'exec-out', 'screencap', '-p'];
    const png = execFileSync('adb', args, { maxBuffer: 64 * 1024 * 1024 });
    writeFileSync(out, png);
}
