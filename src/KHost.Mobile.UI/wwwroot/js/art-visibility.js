// Tells .NET which songs' covers are actually on screen — rendered is not visible; My Songs keeps every
// scrolled-past card in the DOM (see DEVELOPMENT.md → Design notes). One observer covers every surface:
// any element painting a cover carries data-art-song="<song id>".
//
// Track intersecting ELEMENTS, not song ids, and read the id at report time: a surface can swap the song under a
// live element (the roll sheet does on every reroll) without it moving or re-entering the DOM, so the observer
// never fires again and an id-keyed entry would strand the new song as "not visible", cover never fetched.
window.khArtVisibility = {
    _observer: null,
    _ref: null,
    _method: null,
    _visibleEls: new Set(),
    _timer: null,

    // Idempotent: safe to call on every render — later calls pick up elements added since.
    register(dotNetRef, options) {
        this._ref = dotNetRef;
        this._method = options?.method ?? 'VisibleArtChanged';

        if (!this._observer) {
            this._observer = new IntersectionObserver((entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting) this._visibleEls.add(entry.target);
                    else this._visibleEls.delete(entry.target);
                }
                this._flush();
            }, { rootMargin: '100% 0px' });
        }

        for (const el of document.querySelectorAll('[data-art-song]:not([data-art-observed])')) {
            el.setAttribute('data-art-observed', '');
            this._observer.observe(el);
        }
        // Always report after a render, even with no new elements: an existing element may now be showing a
        // different song (a reroll), which changes the answer without changing any intersection.
        this._flush();
    },

    // An element that went away (page change, closed sheet) never gets an "off screen" callback, so drop the
    // disconnected ones here rather than trusting the observer for it.
    _ids() {
        const ids = new Set();
        for (const el of [...this._visibleEls]) {
            if (!el.isConnected) { this._visibleEls.delete(el); continue; }
            const id = el.dataset.artSong;
            if (id) ids.add(id);
        }
        return [...ids];
    },

    // Coalesced: a scroll crosses many elements in a burst, and each would otherwise be its own interop hop.
    _flush() {
        clearTimeout(this._timer);
        this._timer = setTimeout(() => {
            if (this._ref) this._ref.invokeMethodAsync(this._method, this._ids());
        }, 80);
    },

    disconnect() {
        if (this._observer) { this._observer.disconnect(); this._observer = null; }
        this._visibleEls.clear();
        this._ref = null;
    },
};
