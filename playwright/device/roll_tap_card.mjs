// Taps the roll sheet's song card and checks it opens that song's detail sheet.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const { browser, page } = await attach();
await resetToList(page);

await page.locator('.mysongs-fab--surprise').click(TAP);
await page.waitForSelector('.song-card--roll', { timeout: 15000 });

const picked = (await page.locator('.song-card--roll .song-card__title').innerText()).trim();
console.log('rolled:', JSON.stringify(picked));
console.log('card advertises itself as a button:', await page.locator('.song-card--roll').getAttribute('role'));

await page.locator('.song-card--roll').click(TAP);
await page.waitForTimeout(1200);

const opened = await page.evaluate(() => ({
    rollGone: !document.querySelector('.song-card--roll'),
    detailTitle: document.querySelector('.sheet__title')?.textContent.trim(),
    hasRows: !!document.querySelector('.sheet__rows'),
}));
console.log(JSON.stringify(opened));
console.log('opened the picked song:', opened.hasRows && opened.detailTitle === picked);
shot(page, 'roll-tap-card');

await browser.close();
