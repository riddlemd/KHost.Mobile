namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class PerformanceHistorySheet : IDisposable
{
    /// <summary>The song whose history to show. Null hides the sheet.</summary>
    [Parameter] public SongListItem? Item { get; set; }

    /// <summary>Confidence-weighted star for the subtitle; null omits it.</summary>
    [Parameter] public double? BayesScore { get; set; }

    /// <summary>A performance's rating changed: (index into Performances, new value).</summary>
    [Parameter] public EventCallback<(int Index, int Value)> OnRatingChanged { get; set; }

    /// <summary>A performance's note changed: (index into Performances, new text).</summary>
    [Parameter] public EventCallback<(int Index, string? Note)> OnNoteChanged { get; set; }

    /// <summary>A performance's date changed: (index into Performances, new instant).</summary>
    [Parameter] public EventCallback<(int Index, DateTimeOffset Date)> OnDateChanged { get; set; }

    /// <summary>A performance was removed: its index into Performances.</summary>
    [Parameter] public EventCallback<int> OnRemove { get; set; }

    /// <summary>An undo: put the performance back where it was.</summary>
    [Parameter] public EventCallback<(int Index, Performance Performance)> OnRestore { get; set; }

    /// <summary>Raised by the ✕, the backdrop and a pull-down dismiss.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private ElementReference _list;
    private DotNetObjectReference<PerformanceHistorySheet>? _selfRef;
    private bool _bound;              // the list element is rebuilt each open, so this resets on close
    private Guid? _openFor;

    private int UndoWindowMs => Settings.UndoWindowSeconds * 1000;

    private int? _editIndex;
    private string _editDate = string.Empty;
    private Performance? _undone;       // the removal on offer to undo; null = no snackbar
    private int _undoneIndex;
    private CancellationTokenSource? _undoCts;

    protected override void OnParametersSet()
    {
        if (Item?.Id != _openFor)
        {
            _openFor = Item?.Id;
            _editIndex = null;
            ClearUndo();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Item is not null && !_bound)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            // tapMethod is explicitly null: a history row has nothing to open, and the default would call
            // OpenDetailAsync with a performance id.
            await JS.InvokeVoidAsync("khSwipe.register", _list, _selfRef,
                new
                {
                    idAttr = "data-perf-id",
                    swipingClass = "history__item--swiping",
                    tapMethod = (string?)null,
                    removeMethod = nameof(RemoveByIdAsync),
                    holdMethod = nameof(EditDateById),
                });
            _bound = true;
        }
        else if (Item is null)
        {
            _bound = false;
        }
    }

    public void Dispose()
    {
        _undoCts?.Cancel();
        _undoCts?.Dispose();
        _selfRef?.Dispose();
    }

    // The rows are ordered by date, so a gesture reports the performance's own id rather than a position.
    private int IndexOf(string id) =>
        Item is not null && Guid.TryParse(id, out var guid)
            ? Item.Performances.FindIndex(p => p.Id == guid)
            : -1;

    /// <summary>Swipe-left from swipe.js — removes it and offers an undo.</summary>
    [JSInvokable]
    public async Task RemoveByIdAsync(string id)
    {
        var index = IndexOf(id);
        if (index < 0 || Item is null)
            return;

        // Held before the host removes it, so an undo can put back the same object — id, rating, note and all.
        _undone = Item.Performances[index];
        _undoneIndex = index;
        _undoCts?.Cancel();
        _undoCts = new CancellationTokenSource();
        _ = DismissUndoAfterDelayAsync(_undoCts.Token);

        await OnRemove.InvokeAsync(index);
        StateHasChanged();
    }

    private async Task DismissUndoAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(UndoWindowMs, token); }
        catch (TaskCanceledException) { return; }   // superseded by another removal, an undo, or a close

        _undone = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task UndoRemoveAsync()
    {
        var (perf, index) = (_undone, _undoneIndex);
        ClearUndo();
        if (perf is not null)
            await OnRestore.InvokeAsync((index, perf));
    }

    private void ClearUndo()
    {
        _undoCts?.Cancel();
        _undoCts = null;
        _undone = null;
    }

    /// <summary>Press-and-hold from swipe.js — opens the date editor for that performance.</summary>
    [JSInvokable]
    public void EditDateById(string id)
    {
        var index = IndexOf(id);
        if (index < 0 || Item is null)
            return;

        _editIndex = index;
        // datetime-local carries no time zone: hand it the local wall-clock reading of the stored instant.
        _editDate = Item.Performances[index].Date.LocalDateTime.ToString("yyyy-MM-ddTHH:mm");
        StateHasChanged();
    }

    /// <summary>Lets a host route the back button into this sheet's own overlays before closing it.</summary>
    public bool TryCloseTopOverlay()
    {
        if (_editIndex is not null) { CancelEdit(); return true; }
        return false;
    }

    /// <summary>Whether one of this sheet's own overlays is up, so a host can hide its floating controls.</summary>
    public bool HasOverlayOpen => _editIndex is not null;

    private void CancelEdit()
    {
        _editIndex = null;
        _editDate = string.Empty;
    }

    private Task SaveDateAsync(int index)
    {
        if (Item is null || index < 0 || index >= Item.Performances.Count
            || !DateTime.TryParse(_editDate, out var picked))
        {
            CancelEdit();
            return Task.CompletedTask;
        }

        // Keep the offset the performance was logged with: re-reading the current one would shift an older
        // entry by an hour across a DST boundary.
        var offset = Item.Performances[index].Date.Offset;
        CancelEdit();
        return OnDateChanged.InvokeAsync((index, new DateTimeOffset(picked, offset)));
    }
}
