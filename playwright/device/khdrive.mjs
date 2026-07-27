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

// Put the app back on a bare My Songs list: close any sheet a previous script left open and clear the
// search box. Scripts share one long-lived app process, so without this the first click lands on a backdrop.
export async function resetToList(page) {
    // Confirm/editor pop-ups first: they stack above the sheets and have no ✕, so a leftover one (a script that
    // exited mid-flow) leaves a backdrop that silently eats every later click.
    for (let i = 0; i < 4; i++) {
        const cancel = page.locator('.confirm-pop .btn-secondary');
        if (!(await cancel.count())) break;
        await cancel.first().click(TAP).catch(() => {});
        await page.waitForTimeout(400);
    }
    for (let i = 0; i < 4; i++) {
        const close = page.locator('.sheet__close');
        if (!(await close.count())) break;
        await close.first().click(TAP).catch(() => {});
        await page.waitForTimeout(400);
    }
    if (!page.url().endsWith('/')) {
        await page.locator('a[href="/"], a[href=""]').first().click(TAP);
        await page.waitForSelector('.mysongs-fab', { timeout: 10000 });
    }
    const search = page.locator('input[placeholder*="Search" i]');
    if (await search.count()) await search.first().fill('');
    await page.waitForTimeout(400);
}

/** Open the header ⋮ menu and click the item whose text contains `label` (case-insensitive). */
export async function menuTo(page, label) {
    await page.click('.header-menu__btn', TAP);
    await page.getByRole('menuitem', { name: new RegExp(label, 'i') })
        .or(page.locator('.header-menu__item', { hasText: new RegExp(label, 'i') }))
        .first().click(TAP);
}

// Which device adb should talk to, resolved once. KH_SERIAL wins, then ANDROID_SERIAL (which adb honors
// natively, and which the docs use) — otherwise ask adb, and accept the answer only when it's
// unambiguous. Naming the candidates beats letting adb fail per-call with "more than one
// device/emulator" on stderr and an unexplained "Command failed" on the throw.
let _serial;
function deviceArgs() {
    if (_serial === undefined) {
        _serial = process.env.KH_SERIAL || process.env.ANDROID_SERIAL || null;
        if (!_serial) {
            const attached = execFileSync('adb', ['devices'], { encoding: 'utf8' })
                .split('\n').slice(1)
                .map(l => l.trim().split(/\s+/))
                .filter(p => p[1] === 'device')
                .map(p => p[0]);
            if (attached.length === 1) _serial = attached[0];
            else if (attached.length === 0) throw new Error('No device attached — check `adb devices`.');
            else throw new Error(
                `${attached.length} adb transports attached; pick one explicitly:\n` +
                attached.map(s => `  ANDROID_SERIAL=${s} node device/<script>.mjs`).join('\n'));
        }
    }
    return ['-s', _serial];
}

/** The device these helpers are driving — handy for a script that also shells out to adb itself. */
export function serial() {
    return deviceArgs()[1];
}

/**
 * Bring the app to the foreground, e.g. after Android suspended a backgrounded WebView and `attach()`
 * started timing out on the devtools port.
 *
 * NEVER reach for `adb shell monkey` to do this. It looks like a convenient launcher, but it is a random
 * INPUT FUZZER: its event mix includes rotation events, and delivering those can switch the device's
 * auto-rotate setting on. Leave a phone's rotation preference exactly as its owner set it — `am start`
 * does the one thing wanted and touches nothing else.
 *
 * The launchable component is resolved from the device rather than hard-coded, because MAUI derives the
 * activity name (`crc64….MainActivity`) from a hash that moves whenever the namespace or assembly does.
 */
export function foreground() {
    const component = execFileSync('adb', [...deviceArgs(), 'shell', 'cmd', 'package', 'resolve-activity',
        '--brief', 'khost.mobile'], { encoding: 'utf8' }).trim().split('\n').pop().trim();
    if (!component.includes('/')) throw new Error(`could not resolve a launchable activity: "${component}"`);
    execFileSync('adb', [...deviceArgs(), 'shell', 'am', 'start', '-n', component], { encoding: 'utf8' });
    return component;
}

// Save a screenshot under playwright/shots/. Uses `adb exec-out screencap` rather than Playwright's
// page.screenshot — CDP capture hangs on the Android WebView ("fonts loaded" then never returns), and
// the app's WebView is full-screen so the device frame IS the page. `page` is accepted for a uniform
// signature but unused.
export function shot(_page, name) {
    const out = fileURLToPath(new URL(`../shots/${name}.png`, import.meta.url));
    let png;
    try {
        png = execFileSync('adb', [...deviceArgs(), 'exec-out', 'screencap', '-p'], { maxBuffer: 64 * 1024 * 1024 });
    } catch (e) {
        // execFileSync's message is just "Command failed"; adb's actual complaint is on stderr.
        throw new Error(`screencap failed for "${name}": ${String(e.stderr ?? '').trim() || e.message}`);
    }
    writeFileSync(out, png);
}
