// Retitles a song through the detail sheet's edit form and reports whether a ⚠ suggestion follows.
// Reproduces the "I edited the title and no correction was offered" case.
import { attach, resetToList, shot, TAP } from './khdrive.mjs';

const FROM = process.env.KH_FROM;
const TO = process.env.KH_TO;
if (!FROM || !TO) throw new Error('set KH_FROM and KH_TO');

const { browser, page } = await attach();

await resetToList(page);

await page.locator('input[placeholder*="Search" i]').first().fill(FROM);
const card = page.locator('.song-card', { hasText: FROM }).first();
await card.waitFor({ timeout: 15000 });
await card.locator('.song-card__title').click(TAP);

await page.waitForSelector('.sheet__rows', { timeout: 10000 });
await page.getByRole('button', { name: /^edit$/i }).first().click(TAP);
await page.waitForSelector('.sheet input', { timeout: 10000 });

const title = page.locator('.sheet input[type="text"], .sheet input:not([type])').first();
await title.fill(TO);
await page.getByRole('button', { name: /^save$/i }).first().click(TAP);

// The lookup runs after the save; wait for the ⚠ rather than guessing a delay. The offer itself stays
// folded until that mark is tapped, so the mark — not the panel — is what signals a suggestion arrived.
await page.waitForSelector('.sheet__typo', { timeout: 60000 })
    .catch(() => console.log('!! no ⚠ appeared beside the name within 60s'));

const mark = page.locator('.sheet__typo');
console.log('⚠ beside the name:', await mark.count() > 0);
if (await mark.count()) {
    await mark.click(TAP);
    await page.waitForSelector('.typo-hint', { timeout: 5000 });
    console.log('hint text:', (await page.locator('.typo-hint').innerText()).replace(/\s+/g, ' '));
}
shot(page, 'retitle-result');

await browser.close();
