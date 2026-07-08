// Swipe-a-row-left-to-remove for the song table.
// Event-delegated on a stable container (the <tbody>); works with pointer events so it
// covers touch + mouse. Vertical scrolling stays native via `touch-action: pan-y` on the rows.
// As a row slides left it uncovers a red "Remove" strip (a single reusable fixed element)
// sized to the vacated space, so the pending action is always labelled.
window.khSwipe = {
    register(container, dotNetRef) {
        if (!container || container._khSwipeBound) return;
        container._khSwipeBound = true;

        const START_THRESHOLD = 8;    // px of horizontal travel before we treat it as a swipe
        const COMMIT_FRACTION = 0.4;  // swipe past 40% of row width to remove
        const SLIDE_MS = 180;

        let active = null;

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
            const row = e.target.closest('[data-song-id]');
            if (!row) return;
            // Let taps on the interactive controls (favorite, rating, inputs) work normally.
            if (e.target.closest('button, input, select, a, label')) return;

            const rect = row.getBoundingClientRect();
            active = {
                row,
                id: row.getAttribute('data-song-id'),
                startX: e.clientX,
                startY: e.clientY,
                rect,
                dx: 0,
                dragging: false,
                pointerId: e.pointerId,
            };
            row.style.transition = 'none';
        });

        container.addEventListener('pointermove', (e) => {
            if (!active) return;
            const dx = e.clientX - active.startX;
            const dy = e.clientY - active.startY;

            if (!active.dragging) {
                if (Math.abs(dx) < START_THRESHOLD) return;
                if (Math.abs(dx) <= Math.abs(dy)) { active = null; return; }  // vertical intent -> let it scroll
                active.dragging = true;
                active.row.classList.add('song-row--swiping');
                try { active.row.setPointerCapture(active.pointerId); } catch { /* ignore */ }
            }

            const clamped = Math.min(0, dx);   // left only
            active.dx = clamped;
            active.row.style.transform = `translateX(${clamped}px)`;
            showLabel(active.rect, clamped, Math.abs(clamped) > active.rect.width * COMMIT_FRACTION);
        });

        const finish = () => {
            if (!active) return;
            const a = active;
            active = null;
            a.row.classList.remove('song-row--swiping');

            if (!a.dragging) {
                // A clean tap (no horizontal drag) on a non-interactive part of the row opens its detail sheet.
                dotNetRef.invokeMethodAsync('OpenDetail', a.id);
                return;
            }

            if (a.dragging && Math.abs(a.dx) > a.rect.width * COMMIT_FRACTION) {
                // Commit: slide the row off over the fully-revealed strip, then tell .NET to remove.
                showLabel(a.rect, -a.rect.width, true);
                a.row.style.transition = `transform ${SLIDE_MS}ms ease, opacity ${SLIDE_MS}ms ease`;
                a.row.style.transform = `translateX(-${a.rect.width}px)`;
                a.row.style.opacity = '0';
                setTimeout(() => {
                    hideLabel();
                    dotNetRef.invokeMethodAsync('RemoveById', a.id);
                }, SLIDE_MS + 20);
            } else {
                // Snap back.
                hideLabel();
                a.row.style.transition = `transform ${SLIDE_MS}ms ease`;
                a.row.style.transform = 'translateX(0)';
            }
        };

        container.addEventListener('pointerup', finish);
        container.addEventListener('pointercancel', finish);
    },
};
