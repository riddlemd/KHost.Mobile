// Safe-area inset bridge. Android's WebView never fills CSS env(safe-area-inset-*) for the system bars, so the
// native side measures them (ISafeAreaInsets) and MainLayout pushes the values here; app.css layers the variables
// over env() with max(), so platforms where env() already works (iOS) are unaffected.
window.khInsets = {
    set: function (top, bottom) {
        const style = document.documentElement.style;
        style.setProperty('--kh-inset-top', top + 'px');
        style.setProperty('--kh-inset-bottom', bottom + 'px');
    }
};
