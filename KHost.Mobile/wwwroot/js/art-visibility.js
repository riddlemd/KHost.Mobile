// Tells .NET which songs' covers are actually on screen.
//
// WHY: album art used to be fetched for everything *rendered*, and My Songs keeps every card you've scrolled
// past in the DOM — so a long scroll queued hundreds of lookups and held every cover forever. "Rendered" was
// never the right signal; "visible" is, and only the DOM knows it. This is also what makes eviction safe: with
// a real visibility set, the cache can drop what you're not looking at without dropping what you are.
//
// Any element that paints a cover carries data-art-song="<song id>" — cards, set rows, sheet headers alike —
// so one observer covers every surface and a new surface only has to add the attribute.
//
// rootMargin pre-loads a screen's worth either side, so covers are ready by the time a card scrolls in rather
// than popping in after it arrives.
window.khArtVisibility = {
    _observer: null,
    _ref: null,
    _method: null,
    _visible: new Set(),
    _timer: null,

    // Idempotent: safe to call on every render. The first call wires the observer; later calls pick up elements
    // added since (a grown page, a sheet that just opened).
    register(dotNetRef, options) {
        this._ref = dotNetRef;
        this._method = options?.method ?? 'VisibleArtChanged';

        if (!this._observer) {
            this._observer = new IntersectionObserver((entries) => {
                for (const entry of entries) {
                    const id = entry.target.dataset.artSong;
                    if (!id) continue;
                    if (entry.isIntersecting) this._visible.add(id);
                    else this._visible.delete(id);
                }
                this._flush();
            }, { rootMargin: '100% 0px' });
        }

        for (const el of document.querySelectorAll('[data-art-song]:not([data-art-observed])')) {
            el.setAttribute('data-art-observed', '');
            this._observer.observe(el);
        }
        // An element that went away (page change, closed sheet) never gets an "off screen" callback, so drop
        // anything whose element is gone before reporting.
        this._prune();
        this._flush();
    },

    _prune() {
        for (const id of [...this._visible]) {
            const el = document.querySelector(`[data-art-song="${id}"]`);
            if (!el || !el.isConnected) this._visible.delete(id);
        }
    },

    // Coalesced: a scroll crosses many elements in a burst, and each would otherwise be its own interop hop.
    _flush() {
        clearTimeout(this._timer);
        this._timer = setTimeout(() => {
            if (this._ref) this._ref.invokeMethodAsync(this._method, [...this._visible]);
        }, 80);
    },

    disconnect() {
        if (this._observer) { this._observer.disconnect(); this._observer = null; }
        this._visible.clear();
        this._ref = null;
    },
};
