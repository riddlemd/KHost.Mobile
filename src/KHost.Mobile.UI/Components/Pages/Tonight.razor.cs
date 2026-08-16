using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components.Pages;

public sealed partial class Tonight : IDisposable
{
    private IReadOnlyList<SongListItem> _items = [];
    private IReadOnlyList<TonightEntry> _tonight = [];
    private bool _loading = true;

    private ElementReference _list;
    private DotNetObjectReference<Tonight>? _selfRef;
    private bool _dragBound;

    private SongListItem? _removeSong;   // the song whose ✕ (remove) confirm is armed
    private bool _confirmEndNight;

    // Check-off → rating-prompt bridge. The prompt logs a performance; when it commits we flip the matching
    // Tonight row to done (remembering the performance id so an undo removes exactly the one this logged).
    private SongListItem? _ratingPromptItem;
    private Guid? _completingSongId;
    private Guid? _toggling;             // in-flight guard so a double-tap can't double-log a check-off

    // The song whose detail card is open; the card itself is the shared Components/SongDetailSheet.
    private SongListItem? _detailItem;
    private string? _activeVenueKaraFunId;
    private RatingContext _ratingContext = new(null, RatingConfig.Default, default);

    // Scroll is kept on scroll.js so it survives a tab change (the page component is disposed on navigation). It
    // can't be C#-owned, the way My Songs' filter/sort are, because MAUI's Android WebView has no synchronous JS
    // interop to snapshot the offset on dispose.
    private const string ScrollKey = "tonight";
    private bool _scrollRestored;

    protected override async Task OnInitializedAsync()
    {
        // No Tonight tab when the feature is off — this route shouldn't be reachable, so bounce to My Songs.
        if (!Settings.TonightEnabled)
        {
            Nav.NavigateTo("", replace: true);
            return;
        }

        _backGuard = new BackButtonOverlayGuard(BackButton,
            closeTopMost: TryCloseTopOverlay,
            notifyStateChanged: StateHasChanged);
        Store.Changed += OnStoreChanged;
        TonightStore.Changed += OnTonightChanged;
        AlbumArt.Changed += OnArtChanged;   // covers arrive one at a time; paint each as it lands
        await RefreshAsync();
        _loading = false;
        await InvokeAsync(StateHasChanged);
    }

    // Re-wiring the art observer scans the whole DOM, so it only happens when this signature changes.
    private int _itemsVersion;
    private string _artObservedSig = "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // _loading belongs in the signature: the rows live behind it, so a render while still loading has a row
        // count but no elements for the observer to find.
        var artSig = $"{_itemsVersion}:{_tonight.Count}:{_loading}:{_detailItem?.Id}";
        if (artSig != _artObservedSig)
        {
            _artObservedSig = artSig;
            await AlbumArt.ObserveAsync();
        }

        // Attach the listener only AFTER restoring: a tab change fires a mount-time scroll-to-0, and a listener
        // attached earlier would record that 0 and clobber the saved offset. Waiting for !_loading also means the
        // set has painted, so the page has its full height to scroll to.
        if (!_scrollRestored && !_loading)
        {
            _scrollRestored = true;
            await JS.InvokeVoidAsync("khScroll.restore", ScrollKey);
            await JS.InvokeVoidAsync("khScroll.track", ScrollKey);
        }

        // Bind drag-to-reorder on the list body while it's shown; the body remounts when the set empties/refills,
        // so reset the flag when it's gone and re-bind on return.
        if (_tonight.Count > 0 && !_dragBound)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("khTonight.register", _list, _selfRef);
            _dragBound = true;
        }
        else if (_tonight.Count == 0)
        {
            _dragBound = false;
        }
    }

    // Routes the Android back button to close the top-most overlay instead of navigating; active only while one is open.
    private BackButtonOverlayGuard? _backGuard;

    // Closes the single top-most open overlay, matching the CSS stacking order (confirm pop-ups > the rating sheet).
    private bool TryCloseTopOverlay()
    {
        if (_removeSong is not null) { CancelRemove(); return true; }
        if (_confirmEndNight) { CancelEndNight(); return true; }
        if (_ratingPromptItem is not null) { OnRatingCancel(); return true; }
        // The history sheet's own confirm/date pop-ups sit above it, and it sits above the detail card.
        if (_historySheetRef?.TryCloseTopOverlay() == true) return true;
        if (_historyItem is not null) { CloseHistory(); return true; }
        if (_detailItem is not null) { CloseDetail(); return true; }
        return false;
    }

    private void GoToMySongs() => Nav.NavigateTo("");

    // ---- Set list -----------------------------------------------------------------------------

    private int Done => _tonight.Count(e => e.Completed);
    private int Pct => _tonight.Count == 0 ? 0 : (int)Math.Round(Done * 100.0 / _tonight.Count);
    private string ProgressLabel =>
        _tonight.Count == 0 ? "empty"
        : Done == _tonight.Count ? $"all {_tonight.Count} done 🎉"
        : $"{Done} of {_tonight.Count} done";

    // _byId is rebuilt in RefreshAsync, not here: Rows() runs per render, and building a dictionary over the
    // whole library each time cost O(library) to serve a handful of set rows.
    private Dictionary<Guid, SongListItem> _byId = [];
    private IEnumerable<(TonightEntry entry, SongListItem song)> Rows()
    {
        foreach (var entry in _tonight.OrderBy(e => e.Order))
            if (_byId.TryGetValue(entry.SongId, out var song))
                yield return (entry, song);
    }

    private void ArmRemove(SongListItem song) => _removeSong = song;
    private void CancelRemove() => _removeSong = null;

    private async Task ConfirmRemoveAsync()
    {
        var song = _removeSong;
        _removeSong = null;
        if (song is not null)
            await TonightStore.RemoveAsync(song.Id);
    }

    private void AskEndNight() => _confirmEndNight = true;
    private void CancelEndNight() => _confirmEndNight = false;

    private async Task EndNightAsync()
    {
        _confirmEndNight = false;
        await TonightStore.ClearAsync();
    }

    private async Task ToggleDoneAsync(TonightEntry entry, SongListItem song)
    {
        if (_toggling == entry.SongId)
            return;   // a check-off for this row is already running — ignore the double-tap
        _toggling = entry.SongId;
        try
        {
            if (entry.Completed)
            {
                // Undo: remove the exact performance this check-off logged (matched by persisted id, so it works
                // even across an app restart), then clear the flag.
                if (entry.CompletedPerformanceId is { } perfId &&
                    song.Performances.FirstOrDefault(p => p.Id == perfId) is { } perf &&
                    song.Performances.Remove(perf))
                {
                    if (song.Performances.Count == 0)
                        song.Status = SongListItemStatus.WantToSing;
                    await Store.UpdateAsync(song);
                }
                await TonightStore.SetCompletedAsync(entry.SongId, false);
                return;
            }

            if (Settings.RatePerformances)
            {
                // Defer completion to OnRatingCommitAsync (fires when the prompt is saved/skipped/swiped away).
                _completingSongId = entry.SongId;
                _ratingPromptItem = song;
            }
            else
            {
                var perf = new Performance { Date = Clock.GetLocalNow(), HowItWent = 0, VenueId = Session.ActiveVenueId };
                song.Performances.Add(perf);
                song.Status = SongListItemStatus.Sang;
                await Store.UpdateAsync(song);
                await TonightStore.SetCompletedAsync(entry.SongId, true, perf.Id);
            }
        }
        finally
        {
            _toggling = null;
        }
    }

    // ---- Rating prompt (shared component) -----------------------------------------------------

    // Fires on Save, Skip AND swipe-away — all three commit; only ✕ / backdrop cancel.
    private async Task OnRatingCommitAsync((int howItWent, string? note, DateTimeOffset when) result)
    {
        var item = _ratingPromptItem;
        _ratingPromptItem = null;
        if (item is null)
            return;

        var performance = new Performance
        {
            Date = result.when,
            HowItWent = Math.Clamp(result.howItWent, 0, 5),
            Note = string.IsNullOrWhiteSpace(result.note) ? null : result.note.Trim(),
            VenueId = Session.ActiveVenueId,   // tag with wherever they are right now (null when not at a venue)
        };
        item.Performances.Add(performance);
        item.Status = SongListItemStatus.Sang;
        await Store.UpdateAsync(item);

        if (_completingSongId == item.Id)
        {
            await TonightStore.SetCompletedAsync(item.Id, true, performance.Id);
            _completingSongId = null;
        }
    }

    // Dismissed without logging (✕ / backdrop): leave the row unchecked.
    private void OnRatingCancel()
    {
        _ratingPromptItem = null;
        _completingSongId = null;
    }

    // Drag-to-reorder callback from khTonight: the new top-to-bottom order of song ids.
    [JSInvokable]
    public async Task ReorderTonightAsync(string[] orderedIds)
    {
        var guids = orderedIds
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
        await TonightStore.ReorderAsync(guids);
    }

    // ---- Store wiring -------------------------------------------------------------------------

    private void OnStoreChanged(object? sender, EventArgs e) => _ = RefreshOnUiAsync();
    private void OnTonightChanged(object? sender, EventArgs e) => _ = RefreshOnUiAsync();

    private async Task RefreshOnUiAsync()
    {
        try { await InvokeAsync(RefreshAsync); }
        catch (Exception ex)
        {
            // best-effort: keep the last-known set on screen if a reload fails
            Log.LogWarning(ex, "Tonight reload after a store change failed; keeping last-known set");
        }
    }

    private async Task RefreshAsync()
    {
        _items = await Store.GetAllAsync();
        _byId = _items.ToDictionary(i => i.Id);
        _itemsVersion++;
        // The star blends a song's record with the whole list's average, so the context is list-wide, not per song.
        _ratingContext = Scorer.BuildContext(_items,
            RatingConfig.Default with
            {
                RecencyEnabled = Settings.RecencyWeightedRatings,
                PriorWeight = Settings.RatingPriorWeight,
                HalfLifeDays = Settings.RecencyHalfLifeDays,
            },
            Clock.GetLocalNow());
        await RefreshActiveVenueKaraFunAsync();
        await TonightStore.PruneAsync(_items.Select(i => i.Id).ToList());
        _tonight = await TonightStore.GetAllAsync();
        await InvokeAsync(StateHasChanged);
        // No art pre-load here: rendering a row asks the service for its cover, and that request is what fetches it.
    }

    public void Dispose()
    {
        _ = JS.InvokeVoidAsync("khScroll.untrack", ScrollKey);
        Store.Changed -= OnStoreChanged;
        TonightStore.Changed -= OnTonightChanged;
        AlbumArt.Changed -= OnArtChanged;
        _backGuard?.Dispose();
        // No scroll-lock cleanup here: the Sheet component re-reads it from the DOM as each sheet unmounts.
        _selfRef?.Dispose();
    }

    // ---- Song detail card -------------------------------------------------------------------
    private void OpenDetail(SongListItem song)
    {
        _detailItem = song;
    }

    private void CloseDetail() => _detailItem = null;

    private void OnArtChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    // Same confidence-weighted star My Songs shows, so a song reads identically in both places.
    private double? DetailStar => _detailItem is null ? null : Scorer.StarFor(_detailItem, _ratingContext);

    // The row body is a tap target, so give it the keyboard equivalent a real button would have.
    private void OnRowKeyDown(KeyboardEventArgs e, SongListItem song)
    {
        if (e.Key is "Enter" or " ")
            OpenDetail(song);
    }

    private async Task UpdateEnjoymentAsync(int rating)
    {
        if (_detailItem is null)
            return;
        _detailItem.Enjoyment = rating;
        await Store.UpdateAsync(_detailItem);
    }

    // ---- Performance history ------------------------------------------------------------------------
    // The shared sheet reports an index into Performances and this applies it.

    private SongListItem? _historyItem;
    private PerformanceHistorySheet? _historySheetRef;

    private void OpenHistory() => _historyItem = _detailItem;

    private void CloseHistory() => _historyItem = null;

    private async Task UpdatePerformanceRatingAsync(int index, int rating)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].HowItWent = Math.Clamp(rating, 0, 5);
        await Store.UpdateAsync(_historyItem);
    }

    private async Task UpdatePerformanceNoteAsync(int index, string? note)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await Store.UpdateAsync(_historyItem);
    }

    private async Task UpdatePerformanceDateAsync(int index, DateTimeOffset date)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances[index].Date = date;
        await Store.UpdateAsync(_historyItem);
    }

    // The sheet has already offered an undo by the time this runs, so it doesn't close on the last removal —
    // closing would take the undo with it.
    private async Task RemovePerformanceAsync(int index)
    {
        if (_historyItem is null || index < 0 || index >= _historyItem.Performances.Count)
            return;

        _historyItem.Performances.RemoveAt(index);
        if (_historyItem.Performances.Count == 0)
            _historyItem.Status = SongListItemStatus.WantToSing;   // last performance gone — back to unsung
        await Store.UpdateAsync(_historyItem);
    }

    private async Task RestorePerformanceAsync(int index, Performance performance)
    {
        if (_historyItem is null)
            return;

        _historyItem.Performances.Insert(Math.Clamp(index, 0, _historyItem.Performances.Count), performance);
        _historyItem.Status = SongListItemStatus.Sang;
        await Store.UpdateAsync(_historyItem);
    }

    private Task OpenYouTubeAsync() =>
        _detailItem is null ? Task.CompletedTask : Links.OpenAsync(Links2.YouTubeUrlFor(_detailItem.Title, _detailItem.Artist));

    private Task OpenSpotifyAsync() =>
        _detailItem is null ? Task.CompletedTask : Links.OpenAsync(Links2.SpotifyUrlFor(_detailItem.Title, _detailItem.Artist));

    // Only rendered when the active venue has a KaraFun ID, so it's always set here.
    private Task OpenKaraFunAsync() =>
        _detailItem is null || _activeVenueKaraFunId is null
            ? Task.CompletedTask
            : Links.OpenAsync(Links2.KaraFunUrlFor(_activeVenueKaraFunId, _detailItem.Title, _detailItem.Artist));

    private async Task RefreshActiveVenueKaraFunAsync()
    {
        var venue = Session.ActiveVenueId is { } id ? await Venues.GetAsync(id) : null;
        _activeVenueKaraFunId = string.IsNullOrWhiteSpace(venue?.KaraFunVenueId) ? null : venue!.KaraFunVenueId;
    }
}
