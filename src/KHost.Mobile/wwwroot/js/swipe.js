// Row gestures: tap, press-and-hold, and swipe-left-to-remove. Event-delegated on a stable container.
// Vertical scrolling stays native via `touch-action: pan-y` on the rows — don't drop it or the list stops
// scrolling. Shared by the song, venue and singer lists; `options` names the per-list attribute/class/[JSInvokable]
// methods (defaults are the song-table contract), and `holdMethod`/`swipeEnabled` are opt-in per list.
window.khSwipe = {
    register(container, dotNetRef, options) {
        if (!container || container._khSwipeBound) return;
        container._khSwipeBound = true;

        const opts = options || {};
        const idAttr = opts.idAttr || 'data-song-id';
        const swipingClass = opts.swipingClass || 'song-row--swiping';
        // An explicit null opts out of the tap entirely (the sung-history rows have nothing to open); omitting
        // the key keeps the original song-table default.
        const tapMethod = Object.hasOwn(opts, 'tapMethod') ? opts.tapMethod : 'OpenDetailAsync';
        const removeMethod = opts.removeMethod || 'RemoveByIdAsync';
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

        // A completed hold is still followed by a click when the finger lifts, and if the hold opened something
        // under the finger (the sung-history date editor) that click dismisses it instantly. Bound on the
        // document, not the row, because by then the click targets whatever is on top.
        //
        // Armed on release, not when the hold fires: the click follows the lift, so a long hold would outlive a
        // timeout armed at fire time. The timeout only stops a release that draws no click from eating the next one.
        const swallowNextClick = () => {
            const onClick = (e) => { e.preventDefault(); e.stopPropagation(); };
            document.addEventListener('click', onClick, { capture: true, once: true });
            setTimeout(() => document.removeEventListener('click', onClick, { capture: true }), 700);
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

            // One gesture at a time: a second finger landing before the first lifts would strand the first row
            // mid-drag and resolve the release against the wrong row. Kill the first gesture's hold timer (nobody
            // is pressing that row any more) but keep its ownership until it lifts or cancels.
            if (active) {
                cancelHold(active);
                return;
            }

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
            if (!active || e.pointerId !== active.pointerId) return;   // not the finger that owns the gesture
            const dx = e.clientX - active.startX;
            const dy = e.clientY - active.startY;

            // Any real travel in either axis means a scroll or swipe — no longer a candidate tap, and no longer
            // a candidate hold either: a finger that moved was never dwelling in place.
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
            if (!active || e.pointerId !== active.pointerId) return;   // not the finger that owns the gesture
            const a = active;
            active = null;
            cancelHold(a);
            a.row.classList.remove(swipingClass);

            if (a.held)
                swallowNextClick();

            if (!a.dragging) {
                // Only a stationary pointerup that never became a hold is a tap: pointercancel means the browser
                // took the gesture over to scroll, and travel past the slop is a scroll/swipe.
                if (e.type === 'pointerup' && !a.moved && !a.held && tapMethod) {
                    dotNetRef.invokeMethodAsync(tapMethod, a.id);
                }
                return;
            }

            // Commit the removal only on a real release: a pointercancel (OS notification shade, palm rejection,
            // the browser reclaiming the gesture) is not the user choosing to delete — snap the row back instead.
            if (e.type === 'pointerup' && Math.abs(a.dx) > a.rect.width * COMMIT_FRACTION) {
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

        // On the window, not the container: when a hold opens something over the row — the sung-history date
        // editor and its full-screen backdrop — the release lands on that instead, so a container-bound
        // listener never fires and `active` is stranded, killing every later gesture on the list. Each
        // registration's handler ignores pointers it doesn't own, so sharing the window is safe.
        window.addEventListener('pointerup', finish);
        window.addEventListener('pointercancel', finish);
    },
};
