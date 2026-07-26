// Scrolls hard up and down to build an art queue, then parks and watches how long cards sit in the loading
// state. A backlog of songs that scrolled away shows up here as placeholders that stay put for many seconds.
import { evaluate, send, close } from './cdp.mjs';

const CYCLES = Number(process.env.KH_CYCLES || 6);

// Smart landing may open Tonight; this only means anything on the song list.
await evaluate(`[...document.querySelectorAll('a')].find(a => (a.getAttribute('href') ?? '') === '')?.click(); return true`);
for (let i = 0; i < 20 && !(await evaluate(`return !!document.querySelector('.song-card')`)); i++)
    await new Promise(r => setTimeout(r, 400));
await new Promise(r => setTimeout(r, 1500));

const flick = async (up) => {
    const mid = await evaluate(`return { x: innerWidth / 2, y: innerHeight * 0.5 }`);
    const pts = (y) => [{ x: mid.x, y, radiusX: 2, radiusY: 2, force: 1 }];
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: pts(mid.y) });
    for (let d = 40; d <= 420; d += 60) {
        await send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: pts(mid.y + (up ? -d : d)) });
        await new Promise(r => setTimeout(r, 10));
    }
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
};

for (let i = 0; i < CYCLES; i++) {
    await flick(true); await flick(true);
    await new Promise(r => setTimeout(r, 250));
    await flick(false);
    await new Promise(r => setTimeout(r, 250));
}

// Park and watch: on-screen placeholders should resolve quickly once scrolling stops.
console.log('parked — watching the visible loading count:');
for (let i = 1; i <= 20; i++) {
    await new Promise(r => setTimeout(r, 1000));
    const s = await evaluate(`const inView = (e) => { const r = e.getBoundingClientRect();
            return r.bottom > 0 && r.top < innerHeight; };
        const cards = [...document.querySelectorAll('.song-card')];
        const vis = cards.filter(inView);
        return { visible: vis.length,
                 visLoading: vis.filter(c => c.className.includes('kh-art-loading')).length,
                 visArt: vis.filter(c => c.className.includes('song-card--art')).length,
                 totalLoading: cards.filter(c => c.className.includes('kh-art-loading')).length };`);
    console.log(`  +${i}s`, JSON.stringify(s));
    if (s.visLoading === 0) break;
}

close();
process.exit(0);
