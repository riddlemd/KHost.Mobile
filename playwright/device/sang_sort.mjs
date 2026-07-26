// Checks the "Sang" sort: ▼ (the default, like the other date sorts) puts the most recently sung first and
// never-sung songs at the bottom; ▲ brings the never-sung to the top.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const { browser, page } = await attach();
await resetToList(page);

const pick = async (label) => {
    await page.locator('.sort-bar__select').selectOption({ label });
    await page.waitForTimeout(1200);
};

const top = (n = 6) => page.evaluate((count) => [...document.querySelectorAll('.song-card')]
    .slice(0, count)
    .map(c => ({
        title: c.querySelector('.song-card__title')?.textContent.trim(),
        fav: c.querySelector('.fav-btn')?.classList.contains('is-fav') ?? false,
    })), n);

const options = await page.locator('.sort-bar__select option').allTextContents();
console.log('sort options:', JSON.stringify(options));

await pick('Sang');
console.log('direction:', await page.locator('.sort-bar__dir').innerText());
console.log('▼ default (most recently sung first):', JSON.stringify(await top()));
shot(page, 'sang-desc');

// A DOM click, not a synthetic tap: the sticky float bar overlaps the sort row, so a coordinate-based click
// lands on the bar instead of the button.
await page.evaluate(() => document.querySelector('.sort-bar__dir').click());
await page.waitForTimeout(1200);
console.log('direction:', await page.locator('.sort-bar__dir').innerText());
console.log('▲ flipped (never-sung first):        ', JSON.stringify(await top()));
shot(page, 'sang-asc');

await browser.close();
