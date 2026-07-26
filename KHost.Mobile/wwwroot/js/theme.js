// Manual light/dark theme: `data-theme` on <html> wins over the OS `prefers-color-scheme` in app.css.
// An inline script in index.html <head> applies the stored value before first paint to avoid a flash;
// this module only drives the header toggle at runtime — keep the two in sync on the storage key.
window.khTheme = {
    KEY: 'kh-theme',

    current() {
        const stored = localStorage.getItem(this.KEY);
        if (stored === 'light' || stored === 'dark') return stored;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(this.KEY, theme);
    },

    toggle() {
        const next = this.current() === 'dark' ? 'light' : 'dark';
        this.apply(next);
        return next;
    },
};
