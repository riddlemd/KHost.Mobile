// Drag-to-dismiss for the song detail bottom sheet.
// Pull DOWN to close; pulling up does nothing (the page behind is locked, so it can't scroll — the
// old behaviour was the background list scrolling under the open sheet). Internal scrolling is
// respected: the sheet only starts following the finger when it's scrolled to the top and you pull
// down, so a tall sheet still scrolls its own content normally.
window.khSheet = {
    register(sheet, dotNetRef) {
        if (!sheet || sheet._khSheetBound) return;
        sheet._khSheetBound = true;

        // Lock the page behind the sheet (see .kh-sheet-open in app.css).
        document.body.classList.add('kh-sheet-open');

        const START_PX = 6;    // finger travel before we decide drag vs scroll
        const CLOSE_PX = 90;   // pull past this (on release) to dismiss
        const SLIDE_MS = 200;
        const REST = 'translateX(-50%)';   // the sheet's centred resting transform (from CSS)

        let s = null;

        sheet.addEventListener('pointerdown', (e) => {
            // Let taps on interactive controls (buttons, rating, inputs) behave normally.
            if (e.target.closest('button, input, select, textarea, a')) return;
            s = { y: e.clientY, top: sheet.scrollTop, dy: 0, dragging: false, id: e.pointerId };
            sheet.style.transition = 'none';
        });

        sheet.addEventListener('pointermove', (e) => {
            if (!s) return;
            const dy = e.clientY - s.y;
            if (!s.dragging) {
                // Begin dragging only on a downward pull that starts at the top of the sheet's scroll.
                if (dy > START_PX && s.top <= 0) {
                    s.dragging = true;
                    try { sheet.setPointerCapture(s.id); } catch { /* ignore */ }
                } else if (dy < -START_PX || s.top > 0) {
                    s = null;   // upward, or the sheet's own content is scrolling — hand it back
                    return;
                } else {
                    return;
                }
            }
            s.dy = Math.max(0, dy);   // down only; upward travel is ignored
            sheet.style.transform = `${REST} translateY(${s.dy}px)`;
        });

        const finish = () => {
            if (!s) return;
            const cur = s;
            s = null;
            if (!cur.dragging) return;

            if (cur.dy > CLOSE_PX) {
                // Slide it the rest of the way off, then tell .NET to close.
                sheet.style.transition = `transform ${SLIDE_MS}ms ease`;
                sheet.style.transform = `${REST} translateY(100%)`;
                setTimeout(() => dotNetRef.invokeMethodAsync('CloseDetailFromSwipe'), SLIDE_MS - 20);
            } else {
                // Not far enough — snap back to rest.
                sheet.style.transition = `transform ${SLIDE_MS}ms ease`;
                sheet.style.transform = REST;
            }
        };

        sheet.addEventListener('pointerup', finish);
        sheet.addEventListener('pointercancel', finish);
    },

    // Called from .NET when the sheet closes (by any path) so the page scroll unlocks.
    teardown() {
        document.body.classList.remove('kh-sheet-open');
    },
};
