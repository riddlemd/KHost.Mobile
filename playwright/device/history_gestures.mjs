// Drives the sung-history sheet's row gestures: swipe-left arms the remove confirm, press-and-hold on a date
// opens the editor. Real touch points, because swipe.js is a pointer-event state machine.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const { browser, page } = await attach();
await resetToList(page);

const cdp = await page.context().newCDPSession(page);
const touch = (type, x, y) => cdp.send('Input.dispatchTouchEvent', {
    type,
    touchPoints: type === 'touchEnd' ? [] : [{ x, y, id: 1 }],
});
const boxOf = (sel) => page.evaluate((s) => {
    const e = document.querySelector(s);
    if (!e) return null;
    const r = e.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
}, sel);

// Open a song that has history: search one we know was sung, then its detail sheet, then History.
await page.locator('input[placeholder*="Search" i]').first().fill('One armed scissor');
await page.waitForTimeout(1200);
await page.locator('.song-card .song-card__title').first().click(TAP);
await page.waitForSelector('.sheet__rows', { timeout: 10000 });
await page.locator('.sheet__history-link').first().click(TAP);
await page.waitForSelector('.history__item', { timeout: 10000 });

console.log('rows:', await page.locator('.history__item').count());
console.log('✕ buttons left:', await page.locator('.history__remove').count());
console.log('date on one line:', await page.evaluate(() => {
    const d = document.querySelector('.history__date');
    return Math.round(d.getBoundingClientRect().height) <= Math.round(parseFloat(getComputedStyle(d).lineHeight) * 1.2);
}));
shot(page, 'history-rows');

// --- press and hold a date -> the editor opens ---
const date = await boxOf('.history__item .history__date');
await touch('touchStart', date.x + date.w / 2, date.y + date.h / 2);
// Poll rather than sleeping a fixed time: hold (500ms) + interop + render runs close to a second on device.
let opened = false;
for (let t = 0; t < 3000 && !opened; t += 200) {
    await page.waitForTimeout(200);
    opened = (await page.locator('input[type="datetime-local"]').count()) === 1;
}
await touch('touchEnd', date.x + date.w / 2, date.y + date.h / 2);
await page.waitForTimeout(1200);
const stillOpen = (await page.locator('input[type="datetime-local"]').count()) === 1;
console.log('hold -> date editor open:', opened, '| survives the lift:', stillOpen,
    stillOpen ? `value=${await page.locator('input[type="datetime-local"]').inputValue()}` : '');
shot(page, 'history-date-editor');
await page.locator('.confirm-pop .btn-secondary').click(TAP);   // cancel
await page.waitForTimeout(700);

// --- swipe a row left -> it's removed, with an undo offered ---
// Anchored to the date's own box: the note input sits below the row and the stars fill its right half, and
// swipe.js (correctly) ignores both. Dragging the date's width clears the 40%-of-row commit threshold.
const row = await boxOf('.history__item');
const dateBox = await boxOf('.history__date');
const y = dateBox.y + dateBox.h / 2;
const from = dateBox.x + dateBox.w - 10;
await touch('touchStart', from, y);
await page.waitForTimeout(50);
for (let i = 1; i <= 12; i++) {
    await touch('touchMove', from - (from - row.x - 5) * (i / 12), y);
    await page.waitForTimeout(25);
}
await touch('touchEnd', row.x + 5, y);
await page.waitForTimeout(1500);

const afterSwipe = await page.evaluate(() => ({
    rows: document.querySelectorAll('.history__item').length,
    snackbar: document.querySelector('.snackbar__text')?.textContent.trim() ?? null,
    empty: !!document.querySelector('.history__empty'),
}));
console.log('swipe -> removed + undo offered:', JSON.stringify(afterSwipe));
shot(page, 'history-undo');

// Take the undo: the row must come back with its rating and note intact, and nothing persisted as removed.
await page.locator('.snackbar__action').click(TAP);
await page.waitForTimeout(1200);
console.log('after undo -> rows back        :', await page.locator('.history__item').count());

await browser.close();
