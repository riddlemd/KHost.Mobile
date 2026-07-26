// Re-tints the app chrome to the active singer by overriding the brand tokens (--kh-primary*) on <html>.
// The contrast token is pinned white because every color in the singer palette is dark enough for white text.
// Pass a falsy value to clear the override and fall back to the brand accent.
window.khSinger = (function () {
    function clampByte(n) { return Math.max(0, Math.min(255, Math.round(n))); }

    function parseHex(hex) {
        var h = hex.replace('#', '');
        if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
        return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
    }

    function toHex(rgb) {
        return '#' + rgb.map(function (c) { return clampByte(c).toString(16).padStart(2, '0'); }).join('');
    }

    // Mix toward black by `amount` (0..1) — the "strong" (pressed / gradient-dark) end of the accent.
    function darken(hex, amount) {
        return toHex(parseHex(hex).map(function (c) { return c * (1 - amount); }));
    }

    return {
        apply: function (hex) {
            var root = document.documentElement;
            if (!hex) {
                root.style.removeProperty('--kh-primary');
                root.style.removeProperty('--kh-primary-strong');
                root.style.removeProperty('--kh-primary-contrast');
                return;
            }
            root.style.setProperty('--kh-primary', hex);
            root.style.setProperty('--kh-primary-strong', darken(hex, 0.16));
            root.style.setProperty('--kh-primary-contrast', '#ffffff');
        }
    };
})();
