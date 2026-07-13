// First-run tutorial overlay positioning. Blazor (Components/TutorialOverlay.razor) owns the step content and
// visibility; this module just measures the current step's target element and lays out the spotlight hole, the
// caret, and the tip card around it. Mirrors the khScroll/khSheet convention: a window.kh<Feature> object driven
// from the component. `position` returns false when the target isn't on-screen yet, so the component can retry
// (right after a navigation) or fall back to a centered card.
window.khTutorial = {
    _ctx: null,          // last positioned step, so a resize can re-lay-it-out
    _resizeBound: false,

    // Measure `selector` and position hole/caret/tip (all ElementReferences) around it. Returns true if found.
    position(hole, caret, tip, selector) {
        const target = document.querySelector(selector);
        if (!target || target.offsetParent === null) return false;   // not rendered / hidden

        // Bring the target into view first. behavior:'instant' forces a synchronous scroll (overriding any CSS
        // smooth-scroll), so the rect we read next is the target's FINAL position — otherwise a mid-animation
        // rect drops the spotlight far off-screen for anything that needs a big scroll (e.g. a low Settings row).
        target.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'instant' });

        const pad = 6;
        const vw = window.innerWidth, vh = window.innerHeight;
        const r = target.getBoundingClientRect();
        const top = r.top - pad, left = r.left - pad, w = r.width + pad * 2, h = r.height + pad * 2;

        hole.style.top = top + 'px';
        hole.style.left = left + 'px';
        hole.style.width = w + 'px';
        hole.style.height = h + 'px';

        // Place the tip below the hole if it fits, otherwise above. Clamp it to the viewport.
        const tipW = tip.offsetWidth || 300, tipH = tip.offsetHeight || 180;
        const tipLeft = Math.min(Math.max(left + w / 2 - tipW / 2, 12), vw - tipW - 12);
        tip.style.left = tipLeft + 'px';
        tip.style.right = 'auto';
        tip.style.transform = 'none';

        const caretX = Math.min(Math.max(left + w / 2 - 7, tipLeft + 12), tipLeft + tipW - 26);
        caret.style.left = caretX + 'px';

        // Vertical placement: below the hole if it fits, else above, else clamp into the safe band (between the
        // app header and the viewport bottom) with no caret — never let the tip run off-screen, which on a small
        // phone would hide the title/badge behind the status bar (or the Next button off the bottom).
        // The usable band is between the app header's bottom and the app's bottom nav bar (or, when the nav is
        // hidden, just above the OS gesture area) — so the tip never lands under the status bar, the nav tabs, or
        // the gesture bar.
        const header = document.querySelector('.app-header');
        const nav = document.querySelector('.app-nav');
        const safeTop = Math.max(12, header ? header.getBoundingClientRect().bottom + 8 : 12);
        const bottomLimit = (nav ? nav.getBoundingClientRect().top - 8 : vh - 24);
        const gap = 14;
        const spaceBelow = bottomLimit - (top + h);
        const spaceAbove = top - safeTop;

        if (spaceBelow >= tipH + gap) {
            tip.style.top = (top + h + gap) + 'px';
            tip.style.bottom = 'auto';
            caret.style.display = 'block';
            caret.style.top = (top + h + gap - 7) + 'px';
            caret.style.transform = 'rotate(45deg)';
        } else if (spaceAbove >= tipH + gap) {
            tip.style.top = (top - gap - tipH) + 'px';
            tip.style.bottom = 'auto';
            caret.style.display = 'block';
            caret.style.top = (top - gap - 7) + 'px';
            caret.style.transform = 'rotate(225deg)';
        } else {
            // Fits neither side (a tall target on a short screen): pin within the band, drop the caret. If the tip
            // is even taller than the band, keep its bottom (the Next button) on-screen and let the top clip.
            const maxTop = bottomLimit - tipH;
            tip.style.top = (maxTop < safeTop ? maxTop : Math.min(Math.max(top + h + gap, safeTop), maxTop)) + 'px';
            tip.style.bottom = 'auto';
            caret.style.display = 'none';
        }

        this._ctx = { hole, caret, tip, selector };
        if (!this._resizeBound) {
            this._resizeBound = true;
            window.addEventListener('resize', () => {
                const c = this._ctx;
                if (c) this.position(c.hole, c.caret, c.tip, c.selector);
            });
        }
        return true;
    },

    // Strip the inline positioning so a centered step can be laid out purely by CSS (.tutorial--center). Also
    // clears the caret's inline display so the .tutorial--center CSS rule (which hides it) can take over again.
    reset(hole, caret, tip) {
        this._ctx = null;
        tip.style.top = ''; tip.style.left = ''; tip.style.right = ''; tip.style.bottom = ''; tip.style.transform = '';
        caret.style.display = '';
    },

    // Drop the stored resize context (takes no element refs — safe to call after the overlay is torn down).
    clear() {
        this._ctx = null;
    },
};
