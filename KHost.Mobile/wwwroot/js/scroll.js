// Scroll helpers for the song list.
//  - toSong: smoothly reveals a card by id (used after a favorite toggle re-sorts the list).
//  - track/untrack/restore: remember the page's scroll position across SPA navigation. Tab changes don't reload
//    the WebView, so this module-level map outlives the page component being disposed — no interop-on-dispose
//    needed. restore jumps back (instant, so it doesn't animate) once the list has rendered its full height.
//  - track can also report the settled offset to a .NET method, so a page can keep it in its own C# view-state
//    object instead of this module's map; scrollToY restores from such a value.
//
// The commit is DEBOUNCED, not fired per scroll event. That matters for correctness, not just chatter: when a tab
// changes, the browser resets window.scrollY to 0 for the incoming page, firing a 'scroll' event while the old
// page's listener may still be attached. A per-event write would record that 0 and clobber the saved position; a
// debounced write is still pending when the page's untrack cancels it, so the last real position survives.
window.khScroll = {
    _pos: {},
    _handlers: {},
    _timers: {},

    toSong(id) {
        const el = document.querySelector(`[data-song-id="${id}"]`);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },

    track(key, dotnetRef, method) {
        if (this._handlers[key]) return;   // already tracking
        const commit = () => {
            this._pos[key] = window.scrollY;
            if (dotnetRef && method) {
                try { dotnetRef.invokeMethodAsync(method, window.scrollY); } catch (e) { /* ref gone — ignore */ }
            }
        };
        const handler = () => {
            clearTimeout(this._timers[key]);
            this._timers[key] = setTimeout(commit, 120);
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
        clearTimeout(this._timers[key]);   // drop a pending commit (e.g. the navigation reset-to-0)
        delete this._timers[key];
    },

    restore(key) {
        const y = this._pos[key];
        if (y != null) window.scrollTo({ top: y, behavior: 'instant' });
    },

    // Restore to an explicit offset a page kept in its own C# view state (no-op at/above the top).
    scrollToY(y) {
        if (y != null && y > 0) window.scrollTo({ top: y, behavior: 'instant' });
    },
};
