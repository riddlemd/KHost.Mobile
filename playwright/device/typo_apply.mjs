// Opens an already-flagged song and takes the correction. Also proves the suggestion survived a restart:
// this run is a fresh app process from the one that added the song.
import { attach, shot, TAP } from './khdrive.mjs';

const TITLE = process.env.KH_TITLE || 'Bohemian Rapsody';

const { browser, page } = await attach();

if (!page.url().endsWith('/')) {
    await page.locator('a[href="/"], a[href=""]').first().click(TAP);
    await page.waitForSelector('.mysongs-fab', { timeout: 10000 });
}

// Search rather than scroll: the restored scroll position leaves the card rendered but off-screen.
await page.locator('input[placeholder*="Search" i]').first().fill(TITLE);

const card = page.locator('.song-card', { hasText: TITLE }).first();
await card.waitFor({ timeout: 15000 });
console.log('badge survived restart:', (await card.locator('.song-card__typo').count()) === 1);

await card.locator('.song-card__title').click(TAP);
await page.waitForSelector('.typo-hint', { timeout: 10000 });
shot(page, 'apply-1-before');

await page.locator('.typo-hint__apply').click(TAP);

await page.waitForFunction(
    () => !document.querySelector('.typo-hint')
        && !/^Year\s*[—…]\s*$/.test([...document.querySelectorAll('.sheet__row')]
            .find(r => r.textContent.trim().startsWith('Year'))?.textContent.trim() ?? 'Year —'),
    null,
    { timeout: 45000 },
).catch(() => console.log('!! correction did not settle within 45s'));

console.log('title now:', await page.locator('.sheet__title').first().innerText());
console.log('rows:', (await page.locator('.sheet__rows').innerText()).replace(/\s+/g, ' '));
console.log('hint gone:', (await page.locator('.typo-hint').count()) === 0);
shot(page, 'apply-2-after');

await browser.close();
