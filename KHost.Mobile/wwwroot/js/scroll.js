// Scroll helpers for the song list.
//  - toSong: smoothly reveals a card by id (used after a favorite toggle re-sorts the list).
//  - track/untrack/restore: remember the page's scroll position across SPA navigation. Tab changes don't reload
//    the WebView, so this module-level map outlives the page component being disposed — no interop-on-dispose
//    needed. A passive scroll listener records window.scrollY (rAF-throttled) while the page is mounted; restore
//    jumps back to it (instant, so it doesn't animate) once the list has rendered its full height.
window.khScroll = {
    _pos: {},
    _handlers: {},

    toSong(id) {
        const el = document.querySelector(`[data-song-id="${id}"]`);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },

    track(key) {
        if (this._handlers[key]) return;   // already tracking
        let queued = false;
        const handler = () => {
            if (queued) return;
            queued = true;
            requestAnimationFrame(() => {
                queued = false;
                this._pos[key] = window.scrollY;
            });
        };
        this._handlers[key] = handler;
        window.addEventListener('scroll', handler, { passive: true });
    },

    untrack(key) {
        const handler = this._handlers[key];
        if (handler) {
            window.removeEventListener('scroll', handler);
            delete this._handlers[key];
        }
    },

    restore(key) {
        const y = this._pos[key];
        if (y != null) window.scrollTo({ top: y, behavior: 'instant' });
    },
};
