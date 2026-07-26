// Cover images streamed from .NET, exposed as `blob:` URLs (why blob: and not data: — DEVELOPMENT.md → Design notes).
// C# owns the lifecycle: a blob: URL must stay valid as long as the card is rendered, and every re-render re-emits
// the same URL — so DON'T revoke after paint, only on an explicit clear (stale cover, singer switch, page dispose).
window.khAlbumArt = {
    _urls: new Map(),   // songId -> objectURL

    // Revokes any prior cover for the same id — e.g. after a title edit re-fetches a different cover.
    async set(id, streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: 'image/jpeg' }));
        this.revoke(id);
        this._urls.set(id, url);
        return url;
    },

    revoke(id) {
        const url = this._urls.get(id);
        if (url) {
            URL.revokeObjectURL(url);
            this._urls.delete(id);
        }
    },

    revokeAll() {
        for (const url of this._urls.values())
            URL.revokeObjectURL(url);
        this._urls.clear();
    },
};
