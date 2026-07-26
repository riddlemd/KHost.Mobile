// Regression check: album art must survive a My Songs → Tonight → My Songs round trip without needing a
// scroll to come back. Reports covers before leaving and after returning, sampling while it settles.
import { evaluate, close } from './cdp.mjs';

const go = async (href, waitFor) => {
    await evaluate(`[...document.querySelectorAll('a')].find(a => (a.getAttribute('href') ?? '') === '${href}')?.click(); return true`);
    for (let i = 0; i < 20; i++) {
        await new Promise(r => setTimeout(r, 400));
        if (await evaluate(`return !!document.querySelector('${waitFor}')`)) return;
    }
};

const art = () => evaluate(`return {
    path: location.pathname,
    cards: document.querySelectorAll('.song-card').length,
    withArt: document.querySelectorAll('.song-card--art').length,
}`);

await go('', '.song-card');
// Let the first screen's covers land before measuring the baseline.
for (let i = 0; i < 15; i++) {
    await new Promise(r => setTimeout(r, 1000));
    const a = await art();
    if (a.withArt > 0 && i > 3) break;
}
const before = await art();
console.log('before leaving:', JSON.stringify(before));

await go('tonight', '.setrow');
await new Promise(r => setTimeout(r, 2500));
console.log('on tonight:    ', JSON.stringify(await art()));

await go('', '.song-card');
// No scrolling at all — this is the whole point of the check.
for (let i = 1; i <= 6; i++) {
    await new Promise(r => setTimeout(r, 1000));
    console.log(`back +${i}s:      `, JSON.stringify(await art()));
}

close();
process.exit(0);
