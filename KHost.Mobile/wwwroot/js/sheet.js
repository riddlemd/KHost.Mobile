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
    register(sheet, dotNetRef) {
        if (!sheet || sheet._khSheetBound) return;
        sheet._khSheetBound = true;

        // Lock the page behind the sheet (see .kh-sheet-open in app.css).
        document.body.classList.add('kh-sheet-open');

        const DECIDE_PX = 4;   // travel before we commit to drag-vs-scroll
        const CLOSE_PX = 90;   // pull past this (on release) to dismiss
        const SLIDE_MS = 200;
        const REST = 'translateX(-50%)';   // the sheet's centred resting transform (from CSS)

        let s = null;

        const onControl = (t) => t && t.closest && t.closest('button, input, select, textarea, a');

        const begin = (clientY, target) => {
            if (onControl(target)) return;
            s = { y: clientY, top: sheet.scrollTop, dy: 0, dragging: false };
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
                setTimeout(() => dotNetRef.invokeMethodAsync('CloseDetailFromSwipe'), SLIDE_MS - 20);
            } else {
                sheet.style.transition = `transform ${SLIDE_MS}ms ease`;
                sheet.style.transform = REST;   // snap back to rest
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

    // Called from .NET when the sheet closes (by any path) so the page scroll unlocks.
    teardown() {
        document.body.classList.remove('kh-sheet-open');
    },
};
