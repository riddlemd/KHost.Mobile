// <input type=number> accepts 'e'/'E', '+', '-' and '.' — all legal in a floating-point literal, all nonsense in an
// integer field like a release year. Idempotent per element, so it's safe to call on every render.
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
