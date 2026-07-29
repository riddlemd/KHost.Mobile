// Exercises the performance-history sheet's swipe-to-remove on a throwaway song.
//
// What it's watching: a horizontal row swipe must NOT engage khSheet's pull-down-to-dismiss. It samples the
// sheet's transform mid-swipe — any translateY there is the sheet's top "folding down" under the finger.
//
// It creates its own song ("ZZ Swipe Test"), logs performances on it, and deletes the song at the end, so a
// real device's data is left as it was.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const SONG = 'ZZ Swipe Test';
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

const { browser, page } = await attach();
const cdp = await page.context().newCDPSession(page);

// --- real-touch helpers (Blazor's @onclick would bypass swipe.js/khSheet entirely) ------------------
const touch = (type, x, y) => cdp.send('Input.dispatchTouchEvent', {
    type,
    touchPoints: type === 'touchEnd' ? [] : [{ x, y, id: 1, radiusX: 2, radiusY: 2, force: 1 }],
});

const box = async (sel) => page.evaluate((s) => {
    const el = document.querySelector(s);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
}, sel);

/** The history sheet's own transform, so we can tell a pull-down apart from its resting state. */
const sheetTransform = () => page.evaluate(() =>
    document.querySelector('.sheet[aria-label="Performance history"]')?.style.transform || '(none)');

async function report(tag) {
    console.log(`   ${tag}: transform = ${await sheetTransform()}`);
}

// ---------------------------------------------------------------------------------------------------
await resetToList(page);

// Search for it rather than scanning the rendered cards: the list only renders a page at a time, and the
// user's own sort decides where a "ZZ…" title lands.
async function findCard() {
    const search = page.locator('input[placeholder*="Search" i]').first();
    if (await search.inputValue() !== SONG) {
        await search.fill(SONG);
        await page.waitForTimeout(700);
    }
    return page.evaluate((title) => {
        const cards = [...document.querySelectorAll('.song-card')];
        const i = cards.findIndex(c => c.innerText.includes(title));
        return i < 0 ? null : i;
    }, SONG);
}

// --- 1. create the throwaway song ------------------------------------------------------------------
if (await findCard() === null) {
    console.log('1. adding the test song');
    await page.click('.mysongs-fab', TAP);
    await page.waitForSelector('.add-sheet', { timeout: 10000 });
    await page.fill('#title', SONG);
    await page.fill('#artist', 'Test Artist');
    await page.selectOption('#genre', 'Rock');          // filled so no online lookup fires
    await page.fill('#year', '1999');
    await page.click('.add-sheet .btn-primary', TAP);
    await page.waitForTimeout(1200);
} else {
    console.log('1. test song already present');
}

// --- 2. log three performances ---------------------------------------------------------------------
async function openTestSong() {
    const idx = await findCard();
    if (idx === null) throw new Error(`"${SONG}" not in the list`);
    await page.locator('.song-card').nth(idx).click(TAP);
    await page.waitForSelector('.sheet__title', { timeout: 10000 });
    await page.waitForTimeout(500);
}

async function perfCount() {
    return page.evaluate(() => {
        const link = document.querySelector('.sheet__history-count');
        return link ? link.innerText.trim() : '(no history link)';
    });
}

await openTestSong();
for (let i = 0; i < 3; i++) {
    const log = page.locator('.sheet__log-btn').first();   // "Log performance" / "Log another performance"
    if (!(await log.count())) { console.log('   no log-performance button found'); break; }
    await log.click(TAP);
    await page.waitForSelector('.rating-prompt', { timeout: 10000 });
    await page.waitForTimeout(600);   // let the sheet finish sliding up, or the stars are still off-viewport
    await page.locator('.rating-prompt .stars__star').nth(i + 1).click(TAP);   // 2,3,4 stars
    await page.locator('.rating-prompt .btn-primary').click(TAP);
    await page.waitForTimeout(900);
}
console.log(`2. logged performances -> history link says ${await perfCount()}`);

// --- 3. open the history sheet ---------------------------------------------------------------------
await page.locator('.sheet__history-link').first().click(TAP);
await page.waitForSelector('.sheet[aria-label="Performance history"]', { timeout: 10000 });
await page.waitForTimeout(600);
const rows = () => page.locator('.history__item').count();
console.log(`3. history sheet open with ${await rows()} rows`);
shot(page, 'hist-01-three-rows');

// --- 4. swipe a row left, sampling the sheet transform mid-gesture ----------------------------------
async function swipeRowAway(label) {
    const row = await box('.history__item');
    // Start on the date, not the row's right edge: swipe.js ignores pointerdown on a button, and the stars
    // occupy that edge — a swipe begun there never starts.
    const date = await box('.history__item .history__date');
    if (!row || !date) throw new Error('no history row to swipe');
    const y = date.y + date.h / 2;
    const startX = date.x + date.w - 8;

    console.log(`   ${label}: swiping the top row left from x=${Math.round(startX)}, y=${Math.round(y)}`);
    await report('before');
    await touch('touchStart', startX, y);
    // Slight downward drift, exactly like a thumb: this is what used to fold the sheet down.
    for (let i = 1; i <= 8; i++) {
        await touch('touchMove', startX - i * (row.w * 0.08), y + i * 1.5);
        await sleep(35);
        if (i === 4) {
            await report('mid-swipe');
            console.log(`     row transform = ${await page.evaluate(() => document.querySelector('.history__item')?.style.transform || '(none)')}`);
            shot(page, `hist-02-${label}-mid`);
        }
    }
    await touch('touchEnd', 0, 0);
    await report('after release');
    await page.waitForTimeout(1000);
}

await swipeRowAway('three-to-two');
console.log(`   rows now ${await rows()}, snackbar: ${await page.locator('.snackbar').count() ? 'shown' : 'none'}`);
shot(page, 'hist-03-two-rows');

// --- 5. undo it -------------------------------------------------------------------------------------
if (await page.locator('.snackbar__action').count()) {
    await page.locator('.snackbar__action').click(TAP);
    await page.waitForTimeout(800);
    console.log(`5. after undo: ${await rows()} rows`);
    shot(page, 'hist-04-after-undo');
}

// --- 6. delete them all --------------------------------------------------------------------------
console.log('6. deleting the rest');
for (let guard = 0; guard < 8 && await rows() > 0; guard++) {
    await swipeRowAway(`down-from-${await rows()}`);
    console.log(`   rows now ${await rows()}`);
}
console.log('   empty state:', await page.evaluate(() => ({
    empty: document.querySelector('.history__empty')?.innerText ?? null,
    hint: document.querySelector('.history__hint')?.innerText ?? null,
    subtitle: document.querySelector('.sheet[aria-label="Performance history"] .sheet__subtitle')?.innerText ?? null,
    snackbar: document.querySelector('.snackbar')?.innerText ?? null,
})));
shot(page, 'hist-05-empty');

// --- 7. clean up: the device under test is someone's real phone -------------------------------------
if (process.env.KEEP_TEST_SONG !== '1') {
    await resetToList(page);
    const idx = await findCard();
    if (idx !== null) {
        // Start at the CENTRE of the title. The ★ and tonight buttons overlap its right end, and swipe.js
        // ignores a pointerdown that lands on a button — the swipe then silently never starts.
        const card = await page.locator('.song-card').nth(idx).boundingBox();
        const title = await page.locator('.song-card').nth(idx).locator('.song-card__title').boundingBox();
        const y = title.y + title.height / 2, startX = title.x + title.width / 2;
        await touch('touchStart', startX, y);
        for (let i = 1; i <= 10; i++) { await touch('touchMove', startX - i * (card.width * 0.09), y); await sleep(30); }
        await touch('touchEnd', 0, 0);
        await page.waitForTimeout(1200);
        console.log(`7. cleaned up: test song ${await findCard() === null ? 'removed' : 'STILL PRESENT — delete it by hand'}`);
    }
}

await browser.close();
