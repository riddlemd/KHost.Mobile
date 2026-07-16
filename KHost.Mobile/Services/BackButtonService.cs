namespace KHost.Mobile.Services;

/// <summary>
/// Default <see cref="IBackButtonService"/>: a plain LIFO list of overlay-close handlers. Registered as a
/// singleton so every component and the Android back-press callback share the one instance.
/// </summary>
/// <remarks>
/// All access is on the UI thread — components register/unregister from the Blazor renderer thread and the
/// Android <c>OnBackPressedDispatcher</c> fires on that same main thread — so no locking is needed.
/// </remarks>
public sealed class BackButtonService : IBackButtonService
{
    private readonly List<Func<bool>> _handlers = [];

    /// <inheritdoc />
    public IDisposable Register(Func<bool> closeTopMost)
    {
        ArgumentNullException.ThrowIfNull(closeTopMost);
        _handlers.Add(closeTopMost);
        return new Registration(this, closeTopMost);
    }

    /// <inheritdoc />
    public bool HandleBack()
    {
        // Newest-first: the most recently mounted component's overlay sits on top of the stack.
        for (var i = _handlers.Count - 1; i >= 0; i--)
        {
            if (_handlers[i]())
                return true;
        }
        return false;
    }

    private void Unregister(Func<bool> handler) => _handlers.Remove(handler);

    private sealed class Registration(BackButtonService owner, Func<bool> handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            owner.Unregister(handler);
        }
    }
}
