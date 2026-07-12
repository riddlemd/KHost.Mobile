// Drag-to-reorder for the "Tonight" set list.
// Delegated on a stable container; the drag starts only from a row's ⠿ handle (`.setrow__handle`), so tapping
// the check/remove buttons or the row body still works. Pointer events cover touch + mouse. As you drag, the
// lifted row follows the pointer and the others shuffle live via a CSS translate; on drop we hand .NET the new
// order (array of data-song-id strings, top-to-bottom) and let Blazor re-render from the persisted result.
window.khTonight = {
    register(container, dotNetRef) {
        if (!container || container._khTonightBound) return;
        container._khTonightBound = true;

        let drag = null;

        const rows = () => [...container.querySelectorAll('.setrow')];

        const cleanup = () => {
            rows().forEach(r => {
                r.style.transition = '';
                r.style.transform = '';
                r.classList.remove('setrow--dragging');
            });
        };

        container.addEventListener('pointerdown', (e) => {
            const handle = e.target.closest('.setrow__handle');
            if (!handle) return;
            const row = handle.closest('.setrow');
            if (!row) return;
            e.preventDefault();

            const list = rows();
            const rect = row.getBoundingClientRect();
            drag = {
                row,
                pointerId: e.pointerId,
                startY: e.clientY,
                index: list.indexOf(row),
                targetIndex: list.indexOf(row),
                rowH: rect.height + parseFloat(getComputedStyle(row).marginBottom || 0),
            };
            row.classList.add('setrow--dragging');
            row.style.transition = 'none';
            try { handle.setPointerCapture(e.pointerId); } catch { /* ignore */ }
        });

        container.addEventListener('pointermove', (e) => {
            if (!drag) return;
            const dy = e.clientY - drag.startY;
            drag.row.style.transform = `translateY(${dy}px)`;

            // How many slots have we crossed? Round to the nearest row height.
            const list = rows();
            let target = drag.index + Math.round(dy / drag.rowH);
            target = Math.max(0, Math.min(list.length - 1, target));
            drag.targetIndex = target;

            // Shuffle the non-dragged rows to open a gap at the target slot.
            list.forEach((r, i) => {
                if (r === drag.row) return;
                r.style.transition = 'transform 120ms ease';
                let shift = 0;
                if (drag.index < target && i > drag.index && i <= target) shift = -drag.rowH;
                else if (drag.index > target && i >= target && i < drag.index) shift = drag.rowH;
                r.style.transform = shift ? `translateY(${shift}px)` : '';
            });
        });

        const finish = () => {
            if (!drag) return;
            const d = drag;
            drag = null;

            if (d.targetIndex !== d.index) {
                const ids = rows()
                    .filter(r => r !== d.row)
                    .map(r => r.getAttribute('data-song-id'));
                ids.splice(d.targetIndex, 0, d.row.getAttribute('data-song-id'));
                cleanup();
                dotNetRef.invokeMethodAsync('ReorderTonight', ids);
            } else {
                cleanup();   // dropped in place — just snap everything back
            }
        };

        container.addEventListener('pointerup', finish);
        container.addEventListener('pointercancel', finish);
    },
};
