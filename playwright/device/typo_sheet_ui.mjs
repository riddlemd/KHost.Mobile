// Walks the suggestion UI in its new home: ⚠ beside the name in the sheet, folded away until tapped,
// then applied — watching the header for the corrected song's cover.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const TITLE = process.env.KH_TITLE;
if (!TITLE) throw new Error('set KH_TITLE');

const { browser, page } = await attach();
await resetToList(page);

await page.locator('input[placeholder*="Search" i]').first().fill(TITLE);
const card = page.locator('.song-card', { hasText: TITLE }).first();
await card.waitFor({ timeout: 15000 });
await card.locator('.song-card__title').click(TAP);
await page.waitForSelector('.sheet__rows', { timeout: 10000 });

console.log('⚠ beside the name:', await page.locator('.sheet__typo').count());
console.log('panel folded away on open:', (await page.locator('.typo-hint').count()) === 0);
shot(page, 'ui-1-folded');

await page.locator('.sheet__typo').click(TAP);
await page.waitForSelector('.typo-hint', { timeout: 5000 });
console.log('after tapping the ⚠:', (await page.locator('.typo-hint').innerText()).replace(/\s+/g, ' '));
shot(page, 'ui-2-open');

await page.locator('.typo-hint__apply').click(TAP);
for (let i = 0; i < 12; i++) {
    await page.waitForTimeout(1000);
    const s = await page.evaluate(() => {
        const h = document.querySelector('.sheet__header');
        return {
            title: document.querySelector('.sheet__title')?.textContent.trim(),
            art: !!h?.className.includes('sheet__header--art'),
            hint: document.querySelectorAll('.typo-hint').length,
            mark: document.querySelectorAll('.sheet__typo').length,
        };
    });
    console.log(`t+${i + 1}s  title=${JSON.stringify(s.title)} headerArt=${s.art} hint=${s.hint} mark=${s.mark}`);
    if (s.art) break;
}
shot(page, 'ui-3-applied');

await browser.close();
