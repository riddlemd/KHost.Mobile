// Re-shoots docs/screenshots/venues.png — the venue detail sheet, against seeded sample data only.
// See playwright/README.md: a docs frame must never carry a real venue's KaraFun Id.
import { evaluate, tap, close } from './cdp.mjs';

const wait = ms => new Promise(r => setTimeout(r, ms));

await tap('.header-menu__btn');
await wait(400);
await tap('.header-menu__item[href="venues"]');
await wait(900);

console.log('venue rows:', await evaluate(
    `return [...document.querySelectorAll('.venue-row__name, .venue-card__name')].map(e => e.textContent.trim())`));

await tap('.venue-row, .venue-card');
await wait(900);

console.log('sheet open:', await evaluate(`return document.body.classList.contains('kh-sheet-open')`));
console.log('sheet text:', await evaluate(
    `return (document.querySelector('.sheet')?.innerText || '').replace(/\\n+/g, ' | ').slice(0, 400)`));

close();
