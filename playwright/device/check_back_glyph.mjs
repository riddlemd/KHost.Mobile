// Verifies the new ChevronLeftIcon back control on-device: screenshots each page header and measures
// the icon's ink box against the circle's centre.
import { attach, menuTo, shot, TAP, resetToList } from './khdrive.mjs';

const { browser, page } = await attach();

const measure = () => page.evaluate(`(() => {
    const a = document.querySelector('.page-back');
    if (!a) return { error: 'no .page-back on this page' };
    const svg = a.querySelector('svg');
    if (!svg) return { error: 'no svg inside .page-back' };
    const box = a.getBoundingClientRect();
    // getBBox is the path's own ink in user units; map it through the CTM to get on-screen ink.
    const path = svg.querySelector('path');
    const bb = path.getBBox();
    const m = path.getScreenCTM();
    const inkL = m.a * bb.x + m.e, inkT = m.d * bb.y + m.f;
    const inkW = m.a * bb.width, inkH = m.d * bb.height;
    return {
        title: (document.querySelector('.page-title') || {}).textContent,
        circle: { w: +box.width.toFixed(2), h: +box.height.toFixed(2) },
        dX: +((inkL + inkW / 2) - (box.left + box.width / 2)).toFixed(2),
        dY: +((inkT + inkH / 2) - (box.top + box.height / 2)).toFixed(2),
        ink: { w: +inkW.toFixed(2), h: +inkH.toFixed(2) },
        stroke: getComputedStyle(svg).stroke,
    };
})()`);

for (const label of ['Settings', 'Venues', 'Singers', 'About']) {
    await resetToList(page);
    await menuTo(page, label);
    await page.waitForTimeout(600);
    console.log(label, JSON.stringify(await measure()));
    shot(page, `back-glyph-${label.toLowerCase()}`);
}

await browser.close();
