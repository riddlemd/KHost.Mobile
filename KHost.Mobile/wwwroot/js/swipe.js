// Row gestures: tap, press-and-hold, and swipe-left-to-remove. Event-delegated on a stable container; works
// with pointer events so it covers touch + mouse. Vertical scrolling stays native via `touch-action: pan-y`
// on the rows. As a row slides left it uncovers a red "Remove" strip (a single reusable fixed element)
// sized to the vacated space, so the pending action is always labelled.
// Reused by the song, venue and singer lists; `options` names the per-list attribute/class/methods
// (defaults keep the original song-table contract), so each gesture calls that list's own [JSInvokable]
// handlers. `holdMethod` and `swipeEnabled` are opt-in: the singer list wants hold + tap but no swipe.
window.khSwipe = {
    register(container, dotNetRef, options) {
        if (!container || container._khSwipeBound) return;
        container._khSwipeBound = true;

        const opts = options || {};
        const idAttr = opts.idAttr || 'data-song-id';
        const swipingClass = opts.swipingClass || 'song-row--swiping';
        const tapMethod = opts.tapMethod || 'OpenDetail';
        const removeMethod = opts.removeMethod || 'RemoveById';
        const holdMethod = opts.holdMethod || null;
        const holdingClass = opts.holdingClass || 'is-holding';
        const swipeEnabled = opts.swipeEnabled !== false;

        const START_THRESHOLD = 8;    // px of horizontal travel before we treat it as a swipe
        const TAP_SLOP = 10;          // px of movement (either axis) past which it's a scroll/swipe, not a tap
        const COMMIT_FRACTION = 0.4;  // swipe past 40% of row width to remove
        const SLIDE_MS = 180;
        const HOLD_MS = 500;          // matches the platform long-press dwell (iOS/Android context menus)

        let active = null;

        // A hold that already fired owns the gesture: the pointerup that ends it must not also fire the tap,
        // or every long-press would open the sheet it was meant to bypass.
        const cancelHold = (a) => {
            if (!a || !a.holdTimer) return;
            clearTimeout(a.holdTimer);
            a.holdTimer = null;
            a.row.classList.remove(holdingClass);
        };

        const label = (() => {
            let el = document.getElementById('kh-swipe-label');
            if (!el) {
                el = document.createElement('div');
                el.id = 'kh-swipe-label';
                el.innerHTML = '<span class="kh-swipe-label__text">Remove</span> 🗑';
                document.body.appendChild(el);
            }
            return el;
        })();

        const showLabel = (rect, dx, armed) => {
            const curRight = rect.right + dx;                 // row's right edge after translate (dx <= 0)
            label.style.display = 'flex';
            label.style.top = `${rect.top}px`;
            label.style.height = `${rect.height}px`;
            label.style.left = `${curRight}px`;
            label.style.width = `${Math.max(0, rect.right - curRight)}px`;
            label.classList.toggle('is-armed', armed);
        };
        const hideLabel = () => {
            label.style.display = 'none';
            label.classList.remove('is-armed');
        };

        container.addEventListener('pointerdown', (e) => {
            const row = e.target.closest(`[${idAttr}]`);
            if (!row) return;
            // Let taps on the interactive controls (favorite, rating, inputs) work normally.
            if (e.target.closest('button, input, select, a, label')) return;

            // A second finger landing before the first lifts would orphan the previous row's hold timer — it would
            // still fire, for a row the user is no longer pressing, and leave that row's tint stuck on.
            cancelHold(active);

            const rect = row.getBoundingClientRect();
            active = {
                row,
                id: row.getAttribute(idAttr),
                startX: e.clientX,
                startY: e.clientY,
                rect,
                dx: 0,
                dragging: false,
                moved: false,
                held: false,
                holdTimer: null,
                pointerId: e.pointerId,
            };
            row.style.transition = 'none';

            if (holdMethod) {
                const a = active;
                a.holdTimer = setTimeout(() => {
                    a.holdTimer = null;
                    a.held = true;
                    a.row.classList.remove(holdingClass);
                    dotNetRef.invokeMethodAsync(holdMethod, a.id);
                }, HOLD_MS);
                row.classList.add(holdingClass);
            }
        });

        container.addEventListener('pointermove', (e) => {
            if (!active) return;
            const dx = e.clientX - active.startX;
            const dy = e.clientY - active.startY;

            // Any real travel in either axis means this is a scroll or swipe — no longer a candidate tap,
            // and no longer a candidate hold: a finger that moved was never dwelling in place.
            if (Math.abs(dx) > TAP_SLOP || Math.abs(dy) > TAP_SLOP) {
                active.moved = true;
                cancelHold(active);
            }

            if (!swipeEnabled) return;

            if (!active.dragging) {
                if (Math.abs(dx) < START_THRESHOLD) return;
                if (Math.abs(dx) <= Math.abs(dy)) { cancelHold(active); active = null; return; }  // vertical intent -> let it scroll
                cancelHold(active);
                active.dragging = true;
                active.row.classList.add(swipingClass);
                try { active.row.setPointerCapture(active.pointerId); } catch { /* ignore */ }
            }

            const clamped = Math.min(0, dx);   // left only
            active.dx = clamped;
            active.row.style.transform = `translateX(${clamped}px)`;
            showLabel(active.rect, clamped, Math.abs(clamped) > active.rect.width * COMMIT_FRACTION);
        });

        const finish = (e) => {
            if (!active) return;
            const a = active;
            active = null;
            cancelHold(a);
            a.row.classList.remove(swipingClass);

            if (!a.dragging) {
                // Open the detail sheet ONLY on a genuine tap: a stationary pointerup that didn't already become a
                // hold. A pointercancel means the browser took the gesture over to scroll, and any travel past the
                // slop is a scroll/swipe — none of those should open the sheet.
                if (e.type === 'pointerup' && !a.moved && !a.held) {
                    dotNetRef.invokeMethodAsync(tapMethod, a.id);
                }
                return;
            }

            if (a.dragging && Math.abs(a.dx) > a.rect.width * COMMIT_FRACTION) {
                showLabel(a.rect, -a.rect.width, true);
                a.row.style.transition = `transform ${SLIDE_MS}ms ease, opacity ${SLIDE_MS}ms ease`;
                a.row.style.transform = `translateX(-${a.rect.width}px)`;
                a.row.style.opacity = '0';
                setTimeout(() => {
                    hideLabel();
                    dotNetRef.invokeMethodAsync(removeMethod, a.id);
                }, SLIDE_MS + 20);
            } else {
                hideLabel();
                a.row.style.transition = `transform ${SLIDE_MS}ms ease`;
                a.row.style.transform = 'translateX(0)';
            }
        };

        container.addEventListener('pointerup', finish);
        container.addEventListener('pointercancel', finish);
    },
};
