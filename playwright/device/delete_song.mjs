// Deletes ONE song via the list's own swipe-left gesture, after verifying the target is unambiguous.
// Refuses to act unless exactly one card matches the given title AND artist — a mis-targeted swipe here
// destroys real data, and the undo snackbar expires.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const TITLE = process.env.KH_TITLE;
const ARTIST = process.env.KH_ARTIST;
if (!TITLE || !ARTIST) throw new Error('set KH_TITLE and KH_ARTIST');

const { browser, page } = await attach();

await resetToList(page);

await page.locator('input[placeholder*="Search" i]').first().fill(TITLE);
await page.waitForTimeout(1200);

const cards = await page.evaluate(() => [...document.querySelectorAll('.song-card')].map(c => ({
    title: c.querySelector('.song-card__title')?.textContent.trim(),
    artist: c.querySelector('.song-card__artist')?.textContent.trim(),
    id: c.getAttribute('data-song-id'),
})));
console.log('matching cards:', JSON.stringify(cards));

const exact = cards.filter(c => c.title === TITLE && c.artist === ARTIST);
if (cards.length !== 1 || exact.length !== 1) {
    console.log(`REFUSING: need exactly one card matching "${TITLE}" / "${ARTIST}", got ${cards.length} shown / ${exact.length} exact`);
    await browser.close();
    process.exit(1);
}

const box = await page.locator('.song-card').first().boundingBox();
const y = box.y + box.height / 2;
const startX = box.x + box.width - 12;
const endX = box.x + box.width * 0.25;          // ~75% of the width, well past the 40% commit threshold

// Real touch points via CDP: swipe.js is a pointer-event state machine, and a synthetic .click() never
// reaches it (see playwright/README.md).
const cdp = await page.context().newCDPSession(page);
const touch = (type, x) => cdp.send('Input.dispatchTouchEvent', {
    type,
    touchPoints: type === 'touchEnd' ? [] : [{ x, y, id: 1 }],
});

await touch('touchStart', startX);
for (let i = 1; i <= 12; i++) await touch('touchMove', startX + (endX - startX) * (i / 12));
await touch('touchEnd', endX);

await page.waitForFunction(
    (t) => ![...document.querySelectorAll('.song-card__title')].some(e => e.textContent.trim() === t),
    TITLE,
    { timeout: 15000 },
).catch(() => console.log('!! card still present after the swipe'));

shot(page, 'delete-result');
const left = await page.locator('.song-card').count();
console.log(`cards matching the search after delete: ${left}`);

await browser.close();
