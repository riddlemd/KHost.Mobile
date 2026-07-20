// Turns a cover image (streamed from .NET via a DotNetStreamReference) into a `blob:` object URL the card uses as
// its CSS background. Blob URLs are short references — the browser holds the DECODED image behind them — so the
// render tree never carries a base64 copy of every cover (the old data: URI approach). C# (MySongs.razor) owns the
// lifecycle: it calls set() when a cover finishes caching and revoke()/revokeAll() when cards go away, so nothing
// leaks. The URL must stay valid as long as the card is rendered — every re-render re-emits the same blob: URL —
// so we DON'T revoke after paint; only on an explicit clear (stale cover, singer switch, page dispose).
window.khAlbumArt = {
    _urls: new Map(),   // songId -> objectURL

    // Read the streamed bytes, wrap them in a Blob, and hand back a fresh object URL for this song. Replaces (and
    // revokes) any prior cover for the same id — e.g. after a title edit re-fetches a different cover.
    async set(id, streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: 'image/jpeg' }));
        this.revoke(id);
        this._urls.set(id, url);
        return url;
    },

    // Release one song's cover (nothing references its blob: URL any more).
    revoke(id) {
        const url = this._urls.get(id);
        if (url) {
            URL.revokeObjectURL(url);
            this._urls.delete(id);
        }
    },

    // Release every cover — on a singer switch (the whole list is replaced) or when the page is torn down.
    revokeAll() {
        for (const url of this._urls.values())
            URL.revokeObjectURL(url);
        this._urls.clear();
    },
};
