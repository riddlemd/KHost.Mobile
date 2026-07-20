// Walk the whole first-run tutorial and report each card: chapter eyebrow, title, body, the route it
// landed on, and whether a spotlight hole was actually positioned (vs. a centered fallback). Screenshots
// each step. Verifies the reworked Venues cards and the new Singers chapter render + spotlight on-device.
//
// Prereq: app running, `adb forward tcp:9333 localabstract:webview_devtools_remote_<pid>` done.
import { attach, menuTo, shot, TAP } from './khdrive.mjs';
import { mkdirSync } from 'node:fs';

mkdirSync(new URL('./shots/', import.meta.url), { recursive: true });

const { browser, page } = await attach();

// Kick the tour off from Settings → Help → Replay (clears TutorialCompleted + re-arms it).
await menuTo(page, 'Settings');
// The Help section is a collapsible toggle whose open/closed state persists, so only open it if the
// Replay button isn't already showing — a blind click would toggle an already-open section shut.
const replay = page.getByRole('button', { name: 'Replay' });
if (!(await replay.isVisible()))
    await page.getByText('Help', { exact: true }).click(TAP);
await replay.click(TAP);

await page.waitForSelector('.tutorial__tip', { timeout: 10000 });

const seen = [];
for (let i = 0; i < 60; i++) {
    const tip = page.locator('.tutorial__tip');
    await tip.waitFor({ state: 'visible' });
    // Let a just-navigated step settle its spotlight before we read/screenshot.
    await page.waitForTimeout(500);

    const card = await page.evaluate(() => {
        const q = s => document.querySelector(s)?.textContent?.trim() ?? '';
        const hole = document.querySelector('.tutorial__hole');
        const centered = document.querySelector('.tutorial')?.classList.contains('tutorial--center');
        return {
            eyebrow: q('.tutorial__eyebrow'),
            title: q('.tutorial__title'),
            body: q('.tutorial__body').slice(0, 90),
            route: location.pathname,
            spotlight: !centered && hole && hole.offsetWidth > 0,
        };
    });
    seen.push(card);
    const idx = String(i).padStart(2, '0');
    shot(page, `${idx}-${card.title.replace(/[^a-z0-9]+/gi, '-').toLowerCase().slice(0, 30) || 'card'}`);
    console.log(`${idx} | ${card.route.padEnd(16)} | spot:${card.spotlight ? 'Y' : '-'} | ${card.eyebrow} | ${card.title}`);

    const next = page.locator('.tutorial__nav .btn-primary');
    const label = (await next.textContent())?.trim();
    await next.click(TAP);
    if (label === 'Done') break;
}

console.log(`\nTotal cards: ${seen.length}`);
const chapters = [...new Set(seen.map(s => s.eyebrow.split('·')[0].trim()).filter(Boolean))];
console.log('Chapters seen:', chapters.join(' | '));
await browser.close();
