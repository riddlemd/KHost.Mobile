// Tells .NET which songs' covers are actually on screen — rendered is not visible; My Songs keeps every
// scrolled-past card in the DOM (see DEVELOPMENT.md → Design notes).
//
// One observer covers every surface: any element painting a cover carries data-art-song="<song id>".
// rootMargin pre-loads a screen's worth either side, so covers are ready before a card scrolls in.
//
// Intersecting ELEMENTS are what's tracked, not song ids, and the id is read at report time. A surface can
// swap the song under a live element — the roll sheet does exactly that on every reroll — and that element
// neither moves nor re-enters the DOM, so the observer has no reason to fire again. Keyed by id, the new
// song would never be reported visible and its cover would never be fetched. Reading the id late also means
// two surfaces showing the same song can't clobber each other: it's visible if ANY of its elements is.
window.khArtVisibility = {
    _observer: null,
    _ref: null,
    _method: null,
    _visibleEls: new Set(),
    _timer: null,

    // Idempotent: safe to call on every render. The first call wires the observer; later calls pick up elements
    // added since (a grown page, a sheet that just opened).
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

    // The song ids on screen right now. An element that went away (page change, closed sheet) never gets an
    // "off screen" callback, so drop the disconnected ones here rather than trusting the observer for it.
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
