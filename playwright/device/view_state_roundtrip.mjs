// Leaving My Songs and coming back must restore the sort column, its direction, how many pages were grown,
// and the scroll offset — the offset only means anything if the list underneath it is rebuilt identically.
import { attach, resetToList, TAP } from './khdrive.mjs';

const { browser, page } = await attach();
await resetToList(page);

const state = () => page.evaluate(() => ({
    sort: document.querySelector('.sort-bar__select')?.value,
    dir: document.querySelector('.sort-bar__dir')?.textContent.trim(),
    cards: document.querySelectorAll('.song-card').length,
    scrollY: Math.round(window.scrollY),
    firstVisible: (() => {
        const c = [...document.querySelectorAll('.song-card')]
            .find(e => e.getBoundingClientRect().bottom > 0);
        return c?.querySelector('.song-card__title')?.textContent.trim() ?? null;
    })(),
}));
const go = async (href, waitFor) => {
    await page.evaluate((h) => [...document.querySelectorAll('a')]
        .find(a => (a.getAttribute('href') ?? '') === h)?.click(), href);
    for (let i = 0; i < 20; i++) {
        await page.waitForTimeout(400);
        if (await page.evaluate((w) => !!document.querySelector(w), waitFor)) return;
    }
};

await page.locator('.sort-bar__select').selectOption({ label: 'Sang' });
await page.waitForTimeout(1000);
await page.evaluate(() => document.querySelector('.sort-bar__dir').click());   // flip to ▲
await page.waitForTimeout(1000);

// Grow the page and scroll well down, so a wrong rebuild would be obvious.
for (let i = 0; i < 6; i++) {
    await page.evaluate(() => window.scrollBy(0, 2500));
    await page.waitForTimeout(700);
}
const before = await state();
console.log('before leaving:', JSON.stringify(before));

await go('tonight', '.setrow');
await page.waitForTimeout(1500);
await go('', '.song-card');
await page.waitForTimeout(2500);
const after = await state();
console.log('after return  :', JSON.stringify(after));

console.log('sort kept     :', after.sort === before.sort && after.dir === before.dir);
console.log('paging kept   :', after.cards === before.cards);
console.log('scroll kept   :', Math.abs(after.scrollY - before.scrollY) < 200,
    `(${before.scrollY} -> ${after.scrollY})`);
console.log('same song at top:', after.firstVisible === before.firstVisible,
    `(${before.firstVisible} -> ${after.firstVisible})`);

await browser.close();
