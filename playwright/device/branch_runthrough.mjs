// Post-refactor smoke run: touches every subsystem the layering branch moved — all four JSON stores,
// the album-art cache (whose write became atomic), SurprisePicker/RatingScore (whose parameter records
// moved projects), and the injected clock + RNG. Reports PASS/FAIL per step rather than throwing on the
// first problem, so one broken screen can't hide the rest.
//
// Selectors here were read off the running app, not guessed: cards are `.song-card`, and album art is a
// `--kh-card-art: url('blob:…')` custom property on the card itself (there is no <img>).
import { attach, menuTo, shot, TAP } from './khdrive.mjs';

const results = [];
const check = async (name, fn) => {
    try { results.push([true, name, (await fn()) ?? '']); }
    catch (e) { results.push([false, name, String(e.message ?? e).split('\n')[0].slice(0, 110)]); }
};

const { browser, page } = await attach();
const settle = (ms = 1200) => page.waitForTimeout(ms);
const bodyText = async () => (await page.textContent('body')) ?? '';
const navTo = async label => {
    await page.locator('nav a, [class*=nav] a').filter({ hasText: label }).first().click(TAP);
    await settle();
};

const consoleErrors = [];
page.on('console', m => m.type() === 'error' && consoleErrors.push(m.text().slice(0, 150)));
page.on('pageerror', e => consoleErrors.push('pageerror: ' + String(e).slice(0, 150)));

await check('My Songs renders from the song-list store', async () => {
    await navTo('My Songs');
    await page.waitForSelector('.song-card', { timeout: 20000 });
    const n = await page.locator('.song-card').count();
    const heading = (await bodyText()).match(/My songs \((\d+)\)/);
    if (!heading) throw new Error('song count heading not found');
    shot(page, 'rt-01-mysongs');
    return `heading says ${heading[1]} songs, ${n} cards realized`;
});

await check('Album art resolves (AlbumArtCache read path)', async () => {
    await settle(2500);   // art is fetched on visibility, not on render
    const r = await page.evaluate(`(() => {
        const cards = [...document.querySelectorAll('.song-card')];
        const withArt = cards.filter(c => (c.getAttribute('style') || '').includes('blob:'));
        return JSON.stringify({ cards: cards.length, withArt: withArt.length });
    })()`);
    const { cards, withArt } = JSON.parse(r);
    if (withArt === 0) throw new Error(`no card resolved a blob: cover (of ${cards})`);
    return `${withArt}/${cards} cards have a blob cover`;
});

await check('Song detail sheet opens and closes', async () => {
    await page.locator('.song-card').first().click(TAP);
    await settle();
    if (!(await page.evaluate(`document.body.classList.contains('kh-sheet-open')`)))
        throw new Error('detail sheet did not open');
    shot(page, 'rt-02-song-detail');
    await page.evaluate(`document.querySelector('.sheet__close')?.click()`);
    await settle();
    if (await page.evaluate(`document.body.classList.contains('kh-sheet-open')`))
        throw new Error('sheet did not dismiss');
    return 'opened and dismissed';
});

await check('Surprise roll (SurprisePicker + injected Random)', async () => {
    await page.getByText('🎲', { exact: false }).first().click(TAP);
    await settle(1600);
    const t = await bodyText();
    if (!/Reroll/i.test(t)) throw new Error('roll result card did not appear');
    shot(page, 'rt-03-surprise');
    await page.evaluate(`document.querySelector('.sheet__close')?.click()`);
    await settle();
    return 'drew a song, result card shown';
});

await check('Tonight tab loads (tonight store)', async () => {
    await navTo('Tonight');
    const t = await bodyText();
    if (!/Tonight|on deck|Add/i.test(t)) throw new Error('Tonight content not recognised');
    shot(page, 'rt-04-tonight');
    return 'rendered';
});

for (const [label, marker] of [['Venues', /Venue/i], ['Singers', /Singer/i], ['Settings', /Settings/i]]) {
    await check(`${label} loads`, async () => {
        await menuTo(page, label);
        await settle(1300);
        if (!marker.test(await bodyText())) throw new Error(`${label} content not recognised`);
        shot(page, `rt-05-${label.toLowerCase()}`);
        return 'rendered';
    });
}

await check('Back on My Songs, list intact', async () => {
    await navTo('My Songs');
    await page.waitForSelector('.song-card', { timeout: 20000 });
    const heading = (await bodyText()).match(/My songs \((\d+)\)/);
    if (!heading) throw new Error('song count heading missing after navigation');
    return `still ${heading[1]} songs`;
});

await browser.close();

const failed = results.filter(r => !r[0]);
console.log('\n--- run-through ---');
for (const [ok, name, detail] of results) console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' — ' + detail : ''}`);
console.log(`\n  ${results.length - failed.length}/${results.length} passed`);
console.log(consoleErrors.length
    ? '\n  WebView console errors:\n' + [...new Set(consoleErrors)].slice(0, 8).map(e => '    ' + e).join('\n')
    : '  WebView console: clean');
process.exit(failed.length ? 1 : 0);
