// Walk the whole first-run tutorial on the EMULATOR and report each card: chapter eyebrow, title, the route it
// landed on, and whether a spotlight hole was actually positioned (vs. a centered fallback). Screenshots each step
// to ../shots/tour/. The device counterpart is ../device/walk_tutorial.mjs; this one exists because Playwright
// can't attach to the emulator's WebView (see ./cdp.mjs).
//
// Prereqs: the app running, its devtools socket forwarded, and the tour re-armed. Unlike the device walker — which
// drives Settings → Help → Replay — the flag is flipped directly here, since the app must restart to pick it up:
//   see playwright/README.md → Run, for the pull-edit-push sequence (an in-place `run-as … sed -i` on the device
//   mangles the quotes), then relaunch with `dotnet build -t:Run`, forward the port and run this.
import { evaluate, tap, close } from './cdp.mjs';
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const serial = process.env.KH_SERIAL || 'emulator-5554';
const shots = fileURLToPath(new URL('../shots/tour/', import.meta.url));
mkdirSync(shots, { recursive: true });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const readCard = () => evaluate(`
    const q = s => document.querySelector(s)?.textContent?.trim() ?? '';
    const hole = document.querySelector('.tutorial__hole');
    const centered = document.querySelector('.tutorial')?.classList.contains('tutorial--center');
    return {
        present: !!document.querySelector('.tutorial__tip'),
        eyebrow: q('.tutorial__eyebrow'),
        title: q('.tutorial__title'),
        route: location.pathname,
        next: q('.tutorial__nav .btn-primary'),
        spotlight: !centered && !!hole && hole.offsetWidth > 0,
    }`);

await sleep(1500);   // let the overlay arm itself after launch

const seen = [];
for (let i = 0; i < 60; i++) {
    await sleep(700);   // a just-navigated step needs a moment to settle its spotlight
    const card = await readCard();
    if (!card.present) {
        console.log(`${String(i).padStart(2, '0')} | tour ended`);
        break;
    }

    seen.push(card);
    const idx = String(i).padStart(2, '0');
    execFileSync('sh', ['-c', `adb -s ${serial} exec-out screencap -p > ${shots}${idx}.png`]);
    console.log(`${idx} | ${card.route.padEnd(15)} | spot:${card.spotlight ? 'Y' : '-'} | ${card.eyebrow.padEnd(24)} | ${card.title}`);

    await tap('.tutorial__nav .btn-primary');
    if (card.next === 'Done') {
        await sleep(1200);   // let the finish path clear its seeded sample data
        break;
    }
}

const chapters = [...new Set(seen.map((s) => s.eyebrow.split('·')[0].trim()).filter(Boolean))];
console.log(`\nTotal cards: ${seen.length}`);
console.log(`Spotlighted: ${seen.filter((s) => s.spotlight).length} (the rest are centered cards with no anchor)`);
console.log('Chapters seen:', chapters.join(' | '));
close();
