// <input type=number> accepts 'e'/'E' (exponent) plus '+', '-', '.' because they're all
// legal characters in a floating-point number literal. For plain integer fields (a release
// year) that's just confusing, so block any printable key that isn't a digit, and reject
// non-digit pastes. Editing/navigation keys and Ctrl/Cmd shortcuts are left alone.
// Idempotent per element (guarded by a data attribute), so it's safe to call on every render.
window.khNumeric = {
    register() {
        document.querySelectorAll('input.num-year:not([data-num-bound])').forEach((el) => {
            el.setAttribute('data-num-bound', '');
            el.addEventListener('keydown', (e) => {
                if (e.ctrlKey || e.metaKey || e.altKey) return;           // allow copy/paste/select-all
                if (e.key.length === 1 && !/[0-9]/.test(e.key)) e.preventDefault();
            });
            el.addEventListener('paste', (e) => {
                const text = (e.clipboardData || window.clipboardData).getData('text');
                if (/[^0-9]/.test(text)) e.preventDefault();
            });
        });
    },
};
