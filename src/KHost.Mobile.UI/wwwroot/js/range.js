// Dual-handle range slider: stop the two thumbs from crossing. C# clamping isn't enough — re-assigning an
// <input type=range> value from .NET is async and the browser ignores it mid-drag, so the dragged thumb slides
// past the other anyway. Only a *synchronous* `.value` write inside the element's own 'input' handler is honoured.
// Idempotent per element, so it's safe to call on every render.
window.khRange = {
    register() {
        const lo = document.getElementById('f-year-lo');
        const hi = document.getElementById('f-year-hi');
        if (!lo || !hi || lo._khRangeBound) return;
        lo._khRangeBound = true;
        hi._khRangeBound = true;

        lo.addEventListener('input', () => {
            if (parseInt(lo.value, 10) > parseInt(hi.value, 10)) lo.value = hi.value;
        });
        hi.addEventListener('input', () => {
            if (parseInt(hi.value, 10) < parseInt(lo.value, 10)) hi.value = lo.value;
        });
    },
};
