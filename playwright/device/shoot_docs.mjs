// Captures the README screenshot set from a physical device.
//
// Capture is `adb exec-out screencap` (see khdrive.shot) because CDP capture hangs on this WebView —
// so shots are the device's native 1080x2400 including the Android status/nav bars, NOT the old
// 786x1704 Catalyst-preview crop. Navigation is CDP; only the pixels come from adb.
//
// Usage: node device/shoot_docs.mjs [name ...] [--force]   (no args = every shot)
import { attach, shot, TAP } from './khdrive.mjs';

/** The brand violet (SingerColors.Default) — the accent the README grid is shot in. */
const BRAND_ACCENT = '#7c3aed';

const { browser, page } = await attach();
const args = process.argv.slice(2);
const force = args.includes('--force');
const only = args.filter(a => a !== '--force');
const want = name => !only.length || only.includes(name);
const pause = ms => page.waitForTimeout(ms);

/**
 * Theme and accent are set once on the device and then apply silently to every frame, so unlike a
 * wrong-screen shot no per-shot assert can catch them — you find out when you open the PNGs. The
 * active singer's colour overrides --kh-primary across the whole chrome, so shooting under a teal
 * or amber singer yields a grid that doesn't match the app's own branding.
 */
async function preflight() {
    const look = await page.evaluate(`(() => ({
        theme: document.documentElement.getAttribute('data-theme')
            || (window.khTheme ? window.khTheme.current() : ''),
        accent: getComputedStyle(document.documentElement)
            .getPropertyValue('--kh-primary').trim().toLowerCase(),
    }))()`);

    const wrong = [];
    if (look.theme !== 'dark')
        wrong.push(`theme is "${look.theme || 'unset'}", expected dark — set it via ⋮ → 🌙 Dark mode`);
    if (look.accent !== BRAND_ACCENT)
        wrong.push(`accent is "${look.accent || 'unset'}", expected ${BRAND_ACCENT} — switch to a singer on the default violet`);
    if (!wrong.length) return;

    console.error('Refusing to shoot — the device is not set up for the README grid:');
    wrong.forEach(w => console.error(`  • ${w}`));
    console.error('Fix it on the device, or pass --force to shoot anyway.');
    await browser.close();
    process.exit(1);
}

if (!force) await preflight();

/** Back to a bare My Songs list. The tab bar is always present, so it beats hunting a back link. */
async function home() {
    // Chip popovers are NOT sheets and survive a sheet close — a leftover one both eats the next click
    // and photobombs the shot. Toggle them shut via their own chip.
    if (await page.locator('.venue-switcher').count()) { await page.click('.venue-chip', TAP).catch(() => {}); await pause(400); }
    if (await page.locator('.singer-switcher').count()) { await page.click('.singer-chip', TAP).catch(() => {}); await pause(400); }
    for (let i = 0; i < 5; i++) {
        const pop = page.locator('.confirm-pop .btn-secondary');
        if (!(await pop.count())) break;
        await pop.first().click(TAP).catch(() => {});
        await pause(350);
    }
    // Top-down: sheets stack (detail → history), and the covered one's ✕ is behind the upper backdrop.
    for (let i = 0; i < 5; i++) {
        const close = page.locator('.sheet__close');
        if (!(await close.count())) break;
        await close.last().click(TAP).catch(() => {});
        await pause(400);
    }
    const tab = page.locator('.app-nav__tab', { hasText: /My Songs/i });
    if (await tab.count()) await tab.first().click(TAP).catch(() => {});
    await page.waitForSelector('.mysongs-fab', { timeout: 10000 });
    // Clearing the box focuses it, which raises the soft keyboard over half the screen — so only touch it
    // when it actually has text, and always blur afterwards to let the IME retract before the shutter.
    const search = page.locator('input[placeholder*="Search" i]');
    if (await search.count() && await search.first().inputValue()) {
        await search.first().fill('');
        await pause(300);
    }
    await page.evaluate(`document.activeElement && document.activeElement.blur()`);
    await pause(900);
    await page.evaluate(`window.scrollTo(0, 0); document.scrollingElement.scrollTop = 0;`);
    await pause(500);
}

async function menu(label) {
    await page.click('.header-menu__btn', TAP);
    await pause(500);
    await page.locator('.header-menu__item', { hasText: new RegExp(label, 'i') }).first().click(TAP);
    await pause(1100);
}

/** Settings sections are collapsible and remember their state, so open only when shut. */
async function openSection(name) {
    const opened = await page.evaluate(`(() => {
        const btn = [...document.querySelectorAll('.settings-section')]
            .find(b => new RegExp(${JSON.stringify(name)}, 'i').test(b.textContent));
        if (!btn) return 'missing';
        const card = btn.closest('.card');
        const visible = [...card.querySelectorAll('.setting')].some(s => s.offsetParent !== null);
        if (!visible) { btn.click(); return 'opened'; }
        return 'open';
    })()`);
    await pause(700);
    return opened;
}

/** Centre an element in the viewport by its text, so the shot frames the feature. */
async function scrollTo(selector, text) {
    await page.evaluate(`(() => {
        const el = [...document.querySelectorAll(${JSON.stringify(selector)})]
            .find(e => new RegExp(${JSON.stringify(text)}, 'i').test(e.textContent));
        if (el) el.scrollIntoView({ block: 'center' });
    })()`);
    await pause(700);
}

/** Capture only once the screen proves it is what we think — a wrong-screen shot is silent otherwise. */
async function take(name, assert) {
    // The soft keyboard covers half the frame and is invisible to every DOM assert — but visualViewport
    // shrinks while the IME is up, which is the one signal the page can see.
    const ime = await page.evaluate(`(() => {
        const vv = window.visualViewport;
        return vv ? vv.height < window.innerHeight * 0.85 : false;
    })()`);
    if (ime) { console.log(`  ✗ ${name} — soft keyboard is covering the screen`); return; }

    // A stray popover is invisible to a text assert but very visible in the PNG.
    if (!name.endsWith('-switcher')) {
        const stray = await page.evaluate(`!!document.querySelector('.venue-switcher, .singer-switcher')`);
        if (stray) { console.log(`  ✗ ${name} — a chip popover is open over the screen`); return; }
    }
    if (assert) {
        const ok = await page.evaluate(`(() => {
            const sheet = [...document.querySelectorAll('.sheet')].pop();
            const title = sheet ? (sheet.querySelector('.sheet__title') || {}).textContent || '' : '';
            // Whole sheet + whole page: a 400-char sample missed anything below the fold.
            return { title: title.trim(), body: (sheet ? sheet.innerText : '') + '\\n' + document.body.innerText };
        })()`);
        const hit = new RegExp(assert, 'i').test(ok.title) || new RegExp(assert, 'i').test(ok.body);
        if (!hit) { console.log(`  ✗ ${name} — expected /${assert}/, sheet title was "${ok.title}"`); return; }
    }
    shot(page, name);
    console.log('  ✓', name);
}

// ---- the set ------------------------------------------------------------
if (want('my-songs')) { await home(); await take('my-songs'); }

if (want('tonight')) {
    await home();
    await page.locator('.app-nav__tab', { hasText: /Tonight/i }).first().click(TAP);
    await pause(1200);
    await take('tonight', 'tonight');
}

if (want('surprise-me')) {
    await home();
    await page.click('.mysongs-fab--surprise', TAP);
    await page.waitForSelector('.roll-result__primary', { timeout: 15000 });
    await pause(1200);
    await take('surprise-me', 'Add to Tonight|Reroll');
}

if (want('song-detail')) {
    await home();
    await page.locator('.song-card').first().click(TAP);
    await page.waitForSelector('.sheet', { timeout: 10000 });
    await pause(1500);
    await take('song-detail', 'Add to Tonight|Find on YouTube');
}

if (want('lyrics')) {
    await home();
    await page.locator('.song-card').first().click(TAP);
    await page.waitForSelector('.sheet', { timeout: 10000 });
    await pause(1000);
    await page.click('.sheet__lyrics', TAP);
    await pause(4500);
    await take('lyrics', 'lyrics');
}

if (want('performance-history')) {
    await home();
    // Deepest history in this library is 2 performances; the default first card has only 1.
    await page.locator('input[placeholder*="Search" i]').first().fill('favor house');
    await page.evaluate(`document.activeElement && document.activeElement.blur()`);
    await pause(1400);
    await page.locator('.song-card').first().click(TAP);
    await page.waitForSelector('.sheet', { timeout: 10000 });
    await pause(1000);
    // The link's text is the last-sung date, not the word "history" — match the class, not the copy.
    await page.click('.sheet__history-link', TAP);
    await pause(1800);
    await take('performance-history', 'sung|history|performance');
}

if (want('venue-switcher')) { await home(); await page.click('.venue-chip', TAP); await pause(1200); await take('venue-switcher', 'Manage venues'); }
if (want('singer-switcher')) { await home(); await page.click('.singer-chip', TAP); await pause(1200); await take('singer-switcher', "Who's singing|singer"); }

if (want('venues')) {
    await home();
    await menu('Venues');
    await page.locator('.venue-row').first().click(TAP);
    await pause(1500);
    await take('venues', 'venue|performance');
}

if (want('settings')) { await home(); await menu('Settings'); await pause(800); await take('settings', 'Settings'); }

if (want('settings-dials')) {
    await home();
    await menu('Settings');
    console.log('  App section:', await openSection('App'));
    await scrollTo('.setting', 'Undo window');
    // Nudge up so the framed group starts on a whole setting instead of a clipped title.
    await page.evaluate(`document.scrollingElement.scrollTop -= 90`);
    await pause(500);
    await take('settings-dials', 'Undo window');
}

if (want('import-export')) { await home(); await menu('Import'); await pause(1200); await take('import-export', 'import|export'); }

if (want('tutorial')) {
    await home();
    await menu('Settings');
    // Help is collapsible and remembers its state, so a blind click would shut an already-open section.
    const replay = page.getByRole('button', { name: 'Replay' });
    if (!(await replay.count())) { await page.getByText('Help', { exact: true }).click(TAP); await pause(700); }
    await replay.first().click(TAP);
    await page.waitForSelector('.tutorial__tip', { timeout: 15000 });
    await pause(1500);
    await take('tutorial', 'tutorial|next|skip');
    // Leave the tour rather than stranding the app mid-walkthrough.
    const skip = page.locator('.tutorial').getByRole('button', { name: /skip|done|finish/i });
    if (await skip.count()) { await skip.first().click(TAP).catch(() => {}); await pause(900); }
    for (let i = 0; i < 3 && await page.locator('.tutorial__tip').count(); i++) {
        await page.locator('.tutorial').getByRole('button', { name: /skip|done|finish/i })
            .first().click(TAP).catch(() => {});
        await pause(800);
    }
}

await home();
await browser.close();
