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
        const target = this._pos[key] || 0;

        // No saved position for this list (e.g. a singer who hasn't scrolled theirs) → show the top. Without this
        // an unsaved list would keep whatever offset the outgoing list left window.scrollY at.
        if (target <= 0) {
            window.scrollTo({ top: 0, behavior: 'instant' });
            return;
        }

        // A freshly-rendered list (especially after a singer switch) may not have laid out to full height yet in the
        // Android WebView, so a single scrollTo lands short. Re-apply across a few animation frames until the target
        // is actually reachable (window.scrollY catches up) or we give up — then it settles at the true position.
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

// Infinite scroll: watch a sentinel element at the end of the list and, as it nears the viewport, ask .NET to
// render the next page. rootMargin pre-loads before the user hits the very bottom so growth feels seamless. One
// observer at a time (the page has a single list); re-observing swaps the target, disconnect stops it on unmount.
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
