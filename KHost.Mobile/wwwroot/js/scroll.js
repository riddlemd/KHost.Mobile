// Smoothly scrolls a song card into view by its id. Used when the "scroll to song on favorite"
// setting is on: after favoriting re-sorts the list, reveal the song in its new position.
window.khScroll = {
    toSong(id) {
        const el = document.querySelector(`[data-song-id="${id}"]`);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },
};
