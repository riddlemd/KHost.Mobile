// Rolls repeatedly across the WHOLE library and records, per pick, whether the roll sheet ever paints a
// cover — plus how many DOM elements carry that song id. A pick whose only on-screen element is the sheet
// card is the case that matters: nothing else is asking for its cover.
import { attach, resetToList, TAP } from './khdrive.mjs';

const ROLLS = Number(process.env.KH_ROLLS || 10);
const WAIT_S = Number(process.env.KH_WAIT || 6);

const { browser, page } = await attach();
await resetToList(page);

await page.locator('.mysongs-fab--surprise').click(TAP);
await page.waitForSelector('.song-card--roll', { timeout: 15000 });

const rows = [];
for (let r = 0; r < ROLLS; r++) {
    let last = null;
    for (let i = 0; i < WAIT_S; i++) {
        await page.waitForTimeout(1000);
        last = await page.evaluate(() => {
            const roll = document.querySelector('.song-card--roll');
            if (!roll) return null;
            const id = roll.dataset.artSong;
            return {
                id,
                title: roll.querySelector('.song-card__title')?.textContent.trim(),
                els: document.querySelectorAll(`[data-art-song="${id}"]`).length,
                art: roll.className.includes('song-card--art'),
                loading: roll.className.includes('kh-art-loading'),
                visible: [...(window.khArtVisibility?._ids() ?? [])].includes(id),
            };
        });
        if (last?.art) break;
    }
    if (last) rows.push(last);
    await page.locator('.btn', { hasText: 'Reroll' }).first().click(TAP).catch(() => {});
    await page.waitForTimeout(700);
}

console.log(JSON.stringify(rows, null, 1));
console.log('\nno cover after wait:', rows.filter(r => !r.art).map(r => `${r.title} (els=${r.els}, visible=${r.visible})`));

await browser.close();
