// Captures the "Did you mean …?" spelling offer.
//
// Needs a song that HAS a pending suggestion, and a looked-up library has none (every song already
// carries MetadataLookedUp), so this adds a deliberately misspelled song, shoots it, and removes it
// again. Cleanup runs in `finally` — a half-finished run must not leave junk in a real library.
import { attach, shot, TAP } from './khdrive.mjs';

const TITLE = 'Bohemian Rapsody';
const ARTIST = 'Queen';

const { browser, page } = await attach();
const pause = ms => page.waitForTimeout(ms);
const cards = () => page.locator('.song-card').count();

async function home() {
    if (await page.locator('.venue-switcher').count()) { await page.click('.venue-chip', TAP).catch(() => {}); await pause(400); }
    for (let i = 0; i < 5; i++) {
        const c = page.locator('.sheet__close');
        if (!(await c.count())) break;
        await c.last().click(TAP).catch(() => {});
        await pause(400);
    }
    await page.locator('.app-nav__tab', { hasText: /My Songs/i }).first().click(TAP).catch(() => {});
    await page.waitForSelector('.mysongs-fab', { timeout: 10000 });
    await pause(400);
}

const search = async (q) => {
    const box = page.locator('input[placeholder*="Search" i]').first();
    await box.fill(q);
    await pause(1400);
};

/** Real touch drag — swipe.js listens to pointer events, so a DOM click can't trigger the row gesture. */
async function swipeLeft(selector) {
    const cdp = await page.context().newCDPSession(page);
    const box = await page.locator(selector).first().boundingBox();
    const y = box.y + box.height / 2;
    const x0 = box.x + box.width - 20;
    const send = (type, x) => cdp.send('Input.dispatchTouchEvent', {
        type, touchPoints: type === 'touchEnd' ? [] : [{ x, y }],
    });
    await send('touchStart', x0);
    for (let i = 1; i <= 8; i++) { await send('touchMove', x0 - i * 30); await pause(40); }
    await send('touchEnd', x0 - 240);
    await pause(900);
    await cdp.detach();
}

let added = false;
let sort = null;
try {
    await home();

    // Adding a song flips the persisted sort to "Added" so the new row is visible — capture the
    // real state first so cleanup can put it back.
    sort = {
        column: await page.locator('#song-sort').inputValue(),
        dir: (await page.locator('.sort-bar__dir').innerText()).trim(),
    };

    // --- add the misspelled song ---
    await page.click('.mysongs-fab:not(.mysongs-fab--surprise)', TAP);
    await page.waitForSelector('.sheet input.input', { timeout: 10000 });
    await pause(600);
    const inputs = page.locator('.sheet input.input');
    await inputs.nth(0).fill(TITLE);
    await inputs.nth(1).fill(ARTIST);
    await page.getByRole('button', { name: /Add to my songs/i }).click(TAP);
    added = true;
    await pause(2500);

    // --- wait for the lookup to attach a suggestion ---
    await home();
    await search('Rapsody');
    let ok = false;
    for (let i = 0; i < 12; i++) {
        if (!(await cards())) { await pause(2000); continue; }
        await page.locator('.song-card').first().click(TAP);
        await page.waitForSelector('.sheet', { timeout: 8000 });
        await pause(1200);
        if (await page.locator('.sheet__typo').count()) { ok = true; break; }
        await page.locator('.sheet__close').last().click(TAP).catch(() => {});
        await pause(2500);
    }

    if (!ok) {
        console.log('  ✗ spelling-suggestion — no ⚠ appeared (lookup found no near-miss)');
    } else {
        await page.click('.sheet__typo', TAP);      // unfold the "Did you mean …?" offer
        await pause(1200);
        shot(page, 'spelling-suggestion');
        console.log('  ✓ spelling-suggestion');
    }
} finally {
    // --- remove the temp song, whatever happened above ---
    try {
        await home();
        await search('Rapsody');
        if (await cards()) {
            await swipeLeft('.song-card');
            const confirm = page.locator('.confirm-pop').getByRole('button', { name: /delete|remove|yes/i });
            if (await confirm.count()) { await confirm.first().click(TAP); await pause(1200); }
        }
        await search('Rapsody');
        console.log(`  cleanup: ${await cards()} card(s) matching "Rapsody" remain (want 0)`);
        await search('');
        if (sort) {
            await page.selectOption('#song-sort', sort.column);
            await pause(600);
            // Picking a column resets the direction, so restore it separately.
            const dir = (await page.locator('.sort-bar__dir').innerText()).trim();
            if (dir !== sort.dir) { await page.locator('.sort-bar__dir').click(TAP); await pause(600); }
            console.log(`  cleanup: sort restored to ${sort.column} ${sort.dir}`);
        }
    } catch (e) {
        console.log('  !! CLEANUP FAILED — remove the song by hand:', e.message);
    }
    await browser.close();
}
