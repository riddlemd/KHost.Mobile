// Scroll position is remembered here in JS, not in a C# view state, because MAUI's Android WebView has no
// synchronous JS interop and its async JS→C# callbacks don't land reliably — so a page can't pull its scroll
// offset into C# as it's disposed. This module-level map, by contrast, outlives the page (a tab change is SPA
// navigation, not a WebView reload), so the remounted page reads its old position back.
//
// Commits are debounced, not per scroll event, for correctness: a tab change resets window.scrollY to 0 for the
// incoming page, firing a 'scroll' event while the outgoing page's listener may still be attached. A per-event
// write would record that 0 and clobber the saved position; a debounced write is still pending when untrack
// cancels it, so the last real position survives.
window.khScroll = {
    _pos: {},
    _handlers: {},
    _timers: {},

    toSong(id) {
        const el = document.querySelector(`[data-song-id="${id}"]`);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },

    track(key) {
        if (this._handlers[key]) return;
        const commit = () => { this._pos[key] = window.scrollY; };
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
};

// Infinite scroll: watch a sentinel element at the end of the list and, as it nears the viewport, ask .NET to
// render the next page. rootMargin pre-loads before the user hits the very bottom so growth feels seamless. One
// observer at a time (the page has a single list); re-observing swaps the target, disconnect stops it on unmount.
window.khInfinite = {
    _observer: null,

    observe(sentinel, dotNetRef) {
        this.disconnect();
        if (!sentinel) return;
        this._observer = new IntersectionObserver((entries) => {
            if (entries.some(e => e.isIntersecting)) dotNetRef.invokeMethodAsync('LoadMore');
        }, { rootMargin: '600px 0px' });
        this._observer.observe(sentinel);
    },

    disconnect() {
        if (this._observer) { this._observer.disconnect(); this._observer = null; }
    },
};
