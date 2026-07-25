// Reports what a given song's card actually shows right now (badge, art, meta chips).
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const TITLE = process.env.KH_TITLE || 'Dear Maria';

const { browser, page } = await attach();

await resetToList(page);

await page.locator('input[placeholder*="Search" i]').first().fill(TITLE);
const card = page.locator('.song-card', { hasText: TITLE }).first();
await card.waitFor({ timeout: 15000 });

console.log(JSON.stringify({
    title: (await card.locator('.song-card__title').innerText()).trim(),
    artist: await card.locator('.song-card__artist').innerText().catch(() => null),
    typoBadge: await card.locator('.song-card__typo').count(),
    hasArtClass: (await card.getAttribute('class')).includes('song-card--art'),
    meta: (await card.locator('.song-card__meta').innerText()).replace(/\s+/g, ' ').trim(),
}, null, 2));

await card.locator('.song-card__title').click(TAP);
await page.waitForSelector('.sheet__rows', { timeout: 10000 });
console.log('typo hint in sheet:', await page.locator('.typo-hint').count());
shot(page, 'inspect-song');

await browser.close();
