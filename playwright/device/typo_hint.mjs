// Adds a deliberately misspelled song and captures the ⚠ typo hint end-to-end (live iTunes lookup).
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const TITLE = process.env.KH_TITLE || 'Bohemian Rapsody';
const ARTIST = process.env.KH_ARTIST || 'Queen';

const { browser, page } = await attach();

await resetToList(page);

await page.click('.mysongs-fab:not(.mysongs-fab--surprise)', TAP);
await page.waitForSelector('.sheet input', { timeout: 10000 });

const inputs = page.locator('.sheet input[type="text"], .sheet input:not([type])');
await inputs.nth(0).fill(TITLE);
await inputs.nth(1).fill(ARTIST);
shot(page, 'typo-1-add-form');

await page.locator('.sheet .btn-primary').first().click(TAP);

// The lookup runs in the background after the add, so poll the card rather than guessing a delay.
await page.waitForFunction(
    (t) => [...document.querySelectorAll('.song-card')]
        .some(c => c.querySelector('.song-card__title')?.textContent.includes(t)
                && c.querySelector('.song-card__typo')),
    TITLE,
    { timeout: 45000 },
).catch(() => console.log('!! no ⚠ appeared on the card within 45s'));

shot(page, 'typo-2-card');

const card = page.locator('.song-card', { hasText: TITLE }).first();
console.log('badge on card:', await card.locator('.song-card__typo').count());

await card.locator('.song-card__title').click(TAP);
await page.waitForSelector('.typo-hint', { timeout: 10000 })
    .catch(() => console.log('!! no .typo-hint in the detail sheet'));
shot(page, 'typo-3-sheet');

const hint = page.locator('.typo-hint');
if (await hint.count()) console.log('hint text:', (await hint.innerText()).replace(/\s+/g, ' '));

if (process.env.KH_APPLY) {
    await hint.locator('.typo-hint__apply').click(TAP);
    // Applying re-runs the lookup on the corrected text, so wait for the year to arrive, not just the retitle.
    await page.waitForFunction(
        () => !document.querySelector('.typo-hint')
            && !/—|…/.test([...document.querySelectorAll('.sheet__row')]
                .find(r => r.textContent.startsWith('Year'))?.textContent ?? '—'),
        null,
        { timeout: 45000 },
    ).catch(() => console.log('!! correction did not settle within 45s'));

    console.log('title now:', await page.locator('.sheet__title').first().innerText());
    console.log('rows:', (await page.locator('.sheet__rows').innerText()).replace(/\s+/g, ' '));
    shot(page, 'typo-4-applied');
}

await browser.close();
