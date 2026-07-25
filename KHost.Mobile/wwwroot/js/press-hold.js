// Tells a tap apart from a press-and-hold on a single element, and reports each back to .NET.
//
// Why not @onclick plus a C# timer: the WebView fires its own long-press behaviours (text callout, context
// menu, drag) well before a Blazor handler could suppress them, and a click still arrives after a long press
// — so a hold would run and then immediately roll as well. Owning the pointer sequence here lets us cancel
// the click, kill the callout, and drive the hold-progress class off the same state.
//
// Kept separate from swipe.js: that one is a row state machine (tap vs hold vs swipe across a list, with
// horizontal travel and per-list options). This is one element and one axis of decision.
//
// Idempotent per element (guarded by a data attribute), so it's safe to call on every render.
window.khPressHold = {
    register(el, dotNetRef, options) {
        if (!el || el.dataset.pressHoldBound) return;
        el.dataset.pressHoldBound = '';

        const holdMs = options?.holdMs ?? 450;
        const tapMethod = options?.tapMethod;
        const holdMethod = options?.holdMethod;
        const SLOP_PX = 10;   // finger drift that still counts as a press rather than a scroll

        let timer = null, held = false, startX = 0, startY = 0;

        const clear = () => {
            if (timer) { clearTimeout(timer); timer = null; }
            el.classList.remove('is-holding');
        };

        el.addEventListener('pointerdown', (e) => {
            if (e.button && e.button !== 0) return;
            held = false;
            startX = e.clientX;
            startY = e.clientY;
            el.classList.add('is-holding');
            timer = setTimeout(() => {
                held = true;
                clear();
                if (holdMethod) dotNetRef.invokeMethodAsync(holdMethod);
            }, holdMs);
        });

        el.addEventListener('pointermove', (e) => {
            if (!timer) return;
            if (Math.abs(e.clientX - startX) > SLOP_PX || Math.abs(e.clientY - startY) > SLOP_PX) clear();
        });

        el.addEventListener('pointerup', () => {
            const wasArmed = timer !== null;
            clear();
            // Only a press that was still armed counts as a tap: a hold already fired, and a drift-cancelled
            // press should do nothing at all.
            if (wasArmed && !held && tapMethod) dotNetRef.invokeMethodAsync(tapMethod);
        });

        el.addEventListener('pointercancel', clear);
        el.addEventListener('pointerleave', clear);
        // A completed hold is still followed by a click; swallow it so the tap action can't run as well.
        el.addEventListener('click', (e) => { if (held) { e.preventDefault(); e.stopPropagation(); } }, true);
        el.addEventListener('contextmenu', (e) => e.preventDefault());
    },
};
