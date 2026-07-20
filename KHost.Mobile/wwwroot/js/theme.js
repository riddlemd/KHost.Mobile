// Manual light/dark theme. The chosen theme is stamped as `data-theme` on <html> (which
// wins over the OS `prefers-color-scheme` in app.css) and persisted in localStorage so it
// survives restarts. A tiny inline script in index.html <head> applies the stored value
// before first paint to avoid a flash; this module drives the header toggle at runtime.
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
