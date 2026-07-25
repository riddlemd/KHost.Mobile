// Minimal Chrome DevTools Protocol client for driving the app's WebView.
//
// Use this instead of khdrive.mjs on the EMULATOR: its bundled WebView predates the Browser domain that
// Playwright's connectOverCDP calls on connect, so Playwright fails with "Browser context management is not
// supported" before it ever reaches the page. A physical Pixel's WebView is current and works with khdrive.mjs.
//
// Same prerequisite either way — the app running, and its devtools socket forwarded:
//   PID=$(adb -s emulator-5554 shell pidof khost.mobile)
//   adb -s emulator-5554 forward tcp:9333 localabstract:webview_devtools_remote_$PID
const port = process.env.CDP_PORT || 9333;
const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
const target = list.find(t => t.type === 'page');
if (!target) {
    console.error('No page target on the forwarded port. Is the app running and foregrounded?', list);
    process.exit(1);
}

const ws = new WebSocket(target.webSocketDebuggerUrl);   // Node's built-in WebSocket — no ws dependency
let id = 0;
const pending = new Map();
ws.addEventListener('message', (e) => {
    const msg = JSON.parse(e.data);
    if (msg.id && pending.has(msg.id)) {
        pending.get(msg.id)(msg);
        pending.delete(msg.id);
    }
});
await new Promise((resolve) => ws.addEventListener('open', resolve));

/** Send a raw CDP command, e.g. send('Input.dispatchTouchEvent', {...}). */
export function send(method, params = {}) {
    return new Promise((resolve) => {
        const i = ++id;
        pending.set(i, resolve);
        ws.send(JSON.stringify({ id: i, method, params }));
    });
}

/** Run JS in the page and return its value. The body needs an explicit `return`. */
export async function evaluate(body) {
    const r = await send('Runtime.evaluate', { expression: `(() => { ${body} })()`, returnByValue: true, awaitPromise: true });
    if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails));
    return r.result?.result?.value;
}

/** Tap an element by real touch events, so gestures wired through swipe.js / khSheet actually fire. A DOM
 *  .click() reaches Blazor's @onclick but never those, so it can't exercise tap-vs-swipe or pull-to-dismiss. */
export async function tap(selector) {
    const at = await evaluate(`const e = document.querySelector('${selector}');
        if (!e) return null;
        const r = e.getBoundingClientRect();
        return { x: r.x + r.width / 2, y: r.y + Math.min(30, r.height / 2) };`);
    if (!at) throw new Error(`tap: no element for ${selector}`);
    const point = [{ x: at.x, y: at.y, radiusX: 2, radiusY: 2, force: 1 }];
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: point });
    await new Promise((r) => setTimeout(r, 60));
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

/** Pull a sheet down to dismiss it. Default distance clears khSheet's 90px (150px for the taller sheets) threshold. */
export async function swipeDown(selector, distance = 220) {
    const at = await evaluate(`const e = document.querySelector('${selector}');
        if (!e) return null;
        const r = e.getBoundingClientRect();
        return { x: r.x + r.width / 2, y: r.y + 12 };`);
    if (!at) throw new Error(`swipeDown: no element for ${selector}`);
    const points = (y) => [{ x: at.x, y, radiusX: 2, radiusY: 2, force: 1 }];
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: points(at.y) });
    for (let d = 20; d <= distance; d += 40) {
        await send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: points(at.y + d) });
        await new Promise((r) => setTimeout(r, 30));
    }
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

export const close = () => ws.close();
