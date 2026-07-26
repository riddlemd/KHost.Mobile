// Perf verification: scroll deep through a seeded 533-song library with a cold art cache and report what the
// art pipeline did — covers painted, and (via logcat, checked by the caller) discovery vs store-write counts.
import { evaluate, send, close } from './cdp.mjs';

const SWIPES = Number(process.env.KH_SWIPES || 12);

const state = () => evaluate(`return {
    url: location.pathname,
    cards: document.querySelectorAll('.song-card').length,
    withArt: document.querySelectorAll('.song-card--art').length,
    loading: document.querySelectorAll('.kh-art-loading').length,
    scrollY: Math.round(window.scrollY),
}`);

console.log('start:', JSON.stringify(await state()));

// Flick upward (scrolls the list down) with real touch points so the browser drives its own momentum.
async function flick() {
    const mid = await evaluate(`return { x: innerWidth / 2, y: innerHeight * 0.75 }`);
    const pts = (y) => [{ x: mid.x, y, radiusX: 2, radiusY: 2, force: 1 }];
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: pts(mid.y) });
    for (let d = 40; d <= 520; d += 60) {
        await send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: pts(mid.y - d) });
        await new Promise((r) => setTimeout(r, 12));
    }
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

for (let i = 0; i < SWIPES; i++) {
    await flick();
    await new Promise((r) => setTimeout(r, 900));
    if ((i + 1) % 4 === 0) console.log(`after ${i + 1} swipes:`, JSON.stringify(await state()));
}

// Let in-flight discovery finish its paced drain before the final reading.
await new Promise((r) => setTimeout(r, 12000));
console.log('settled:', JSON.stringify(await state()));

close();
