// Scroll position lives here in JS, not in a C# view state: MAUI's Android WebView has no synchronous JS interop
// and its async JS→C# callbacks don't land reliably, so a page can't pull its offset into C# as it's disposed.
//
// Commits are debounced for correctness, not perf: a tab change resets window.scrollY to 0 while the outgoing
// page's listener may still be attached, and a per-event write would clobber the saved position with that 0.
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
        const target = this._pos[key] || 0;

        // Explicit top for an unsaved list (e.g. a singer who hasn't scrolled theirs) — otherwise it keeps whatever
        // offset the outgoing list left window.scrollY at.
        if (target <= 0) {
            window.scrollTo({ top: 0, behavior: 'instant' });
            return;
        }

        // A freshly-rendered list (especially after a singer switch) may not have laid out to full height yet in the
        // Android WebView, so a single scrollTo lands short. Re-apply until the target is actually reachable.
        let tries = 0;
        const apply = () => {
            window.scrollTo({ top: target, behavior: 'instant' });
            if (Math.abs(window.scrollY - target) > 2 && tries++ < 20) {
                requestAnimationFrame(apply);
            }
        };
        requestAnimationFrame(apply);
    },

    toTop() { window.scrollTo({ top: 0, behavior: 'instant' }); },
};

// Infinite scroll: a sentinel at the end of the list asks .NET for the next page as it nears the viewport.
// One observer at a time (the page has a single list) — re-observing swaps the target.
window.khInfinite = {
    _observer: null,
    _suspended: false,
    _timer: null,

    observe(sentinel, dotNetRef) {
        this.disconnect();
        if (!sentinel) return;
        this._observer = new IntersectionObserver((entries) => {
            // Suspended briefly after a page-1 reset so the shrink-content reflow (which momentarily parks the
            // sentinel in view before the scroll-to-top lands) doesn't trip a spurious extra load.
            if (this._suspended) return;
            if (entries.some(e => e.isIntersecting)) dotNetRef.invokeMethodAsync('LoadMore');
        }, { rootMargin: '600px 0px' });
        this._observer.observe(sentinel);
    },

    suspend(ms) {
        this._suspended = true;
        clearTimeout(this._timer);
        this._timer = setTimeout(() => { this._suspended = false; }, ms || 400);
    },

    disconnect() {
        if (this._observer) { this._observer.disconnect(); this._observer = null; }
    },
};
