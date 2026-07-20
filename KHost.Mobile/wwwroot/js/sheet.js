// Drag-to-dismiss for the song detail bottom sheet.
// Pull DOWN to close; pulling up does nothing (the page behind is locked, so it can't scroll — the
// old behaviour was the background list scrolling under the open sheet). Internal scrolling is
// respected: the drag only takes over when the sheet is scrolled to its top and you pull down, so a
// tall sheet still scrolls its own content normally.
//
// Touch is handled with non-passive touch events so we can preventDefault() and take the gesture
// away from the browser's native scroll/overscroll — with pointer events the browser claims a
// downward pull for its overscroll bounce and fires pointercancel, so the sheet never closed. Mouse
// (desktop / Windows head) is handled separately via pointer events.
window.khSheet = {
    register(sheet, dotNetRef, options) {
        if (!sheet || sheet._khSheetBound) return;
        sheet._khSheetBound = true;

        const opts = options || {};
        const closeMethod = opts.closeMethod || 'CloseDetailFromSwipe';

        // NB: the page-scroll lock (.kh-sheet-open) is driven from Blazor via setLock() below, not added here,
        // so it can't get stranded when a sheet closes on a path that skips teardown (e.g. a touch swipe-dismiss).

        const DECIDE_PX = 4;                  // travel before we commit to drag-vs-scroll
        const CLOSE_PX = opts.closePx || 90;  // pull past this (on release) to dismiss; bigger = a stronger swipe
        const SLIDE_MS = 200;
        const REST = 'translateX(-50%)';      // the sheet's centred resting transform (from CSS)

        let s = null;

        const onControl = (t) => t && t.closest && t.closest('button, input, select, textarea, a');

        // A sheet can have its own inner scroller (e.g. the history list); drag-to-dismiss only engages when
        // THIS one is at its top, so a scrolled-down list keeps scrolling instead of the pull dragging the
        // sheet closed.
        const scrollerFor = (target) => {
            let el = target;
            while (el && el !== sheet.parentElement) {
                const oy = getComputedStyle(el).overflowY;
                if ((oy === 'auto' || oy === 'scroll') && el.scrollHeight > el.clientHeight) return el;
                if (el === sheet) break;
                el = el.parentElement;
            }
            return sheet;
        };

        const begin = (clientY, target) => {
            if (onControl(target)) return;
            s = { y: clientY, top: scrollerFor(target).scrollTop, dy: 0, dragging: false };
            sheet.style.transition = 'none';
        };

        // Returns true when it consumed the move (caller should preventDefault for touch).
        const move = (clientY) => {
            if (!s) return false;
            const dy = clientY - s.y;
            if (!s.dragging) {
                if (Math.abs(dy) < DECIDE_PX) return false;
                // Take over ONLY for a downward pull that starts at the top; otherwise hand back to
                // native scrolling (upward, or the sheet's own content is scrolled).
                if (dy < 0 || s.top > 0) { s = null; return false; }
                s.dragging = true;
            }
            s.dy = Math.max(0, dy);   // down only; upward travel is ignored
            sheet.style.transform = `${REST} translateY(${s.dy}px)`;
            return true;
        };

        const end = () => {
            if (!s) return;
            const cur = s;
            s = null;
            if (!cur.dragging) return;
            if (cur.dy > CLOSE_PX) {
                sheet.style.transition = `transform ${SLIDE_MS}ms ease`;
                sheet.style.transform = `${REST} translateY(100%)`;
                setTimeout(() => dotNetRef.invokeMethodAsync(closeMethod), SLIDE_MS - 20);
            } else {
                sheet.style.transition = `transform ${SLIDE_MS}ms ease`;
                sheet.style.transform = REST;
            }
        };

        // --- Touch (the real target): non-passive so we can cancel native scroll. ---
        sheet.addEventListener('touchstart', (e) => {
            begin(e.touches[0].clientY, e.target);
        }, { passive: true });
        sheet.addEventListener('touchmove', (e) => {
            if (move(e.touches[0].clientY) && e.cancelable) e.preventDefault();
        }, { passive: false });
        sheet.addEventListener('touchend', end);
        sheet.addEventListener('touchcancel', end);

        // --- Mouse fallback (desktop / Windows head). Ignore touch-synthesised pointer events. ---
        sheet.addEventListener('pointerdown', (e) => { if (e.pointerType === 'mouse') begin(e.clientY, e.target); });
        sheet.addEventListener('pointermove', (e) => { if (e.pointerType === 'mouse') move(e.clientY); });
        sheet.addEventListener('pointerup', (e) => { if (e.pointerType === 'mouse') end(); });
    },

    // Page-scroll lock behind sheets, driven from Blazor by whether ANY sheet is open. Because it's set from
    // component state on every render (not toggled per open/close), it can never get stranded on a close path
    // that skips cleanup — the touch swipe-dismiss bug that stuck the lock on and killed scrolling everywhere.
    setLock(on) {
        document.body.classList.toggle('kh-sheet-open', !!on);
    },
};
