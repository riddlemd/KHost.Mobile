using KHost.Mobile.Abstractions.Clients.Search;
using KHost.Mobile.Common.Text;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile.UI.Components;

public sealed partial class SongSearchPanel : IDisposable
{
    /// <summary>What's typed in the title field — the search.</summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>What's typed in the artist field — narrows the search when not blank.</summary>
    [Parameter] public string Artist { get; set; } = string.Empty;

    /// <summary>The singer's song list, searched first and used to spot an online hit they already have.</summary>
    [Parameter] public IReadOnlyList<SongListItem> Library { get; set; } = [];

    /// <summary>A song from the online catalogue was chosen.</summary>
    [Parameter] public EventCallback<SongSearchHit> OnPick { get; set; }

    /// <summary>A song already in the list was chosen — open it rather than add a duplicate.</summary>
    [Parameter] public EventCallback<SongListItem> OnOpen { get; set; }

    // Long enough that a word typed at speed is one request, short enough to feel live.
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);
    private const int LocalLimit = 3;
    private const int OnlineLimit = 6;
    private const int MinimumLength = 2;

    private IReadOnlyList<SongListItem> _local = [];
    private IReadOnlyList<SongSearchHit>? _hits;   // null = no online answer for the current query (yet, or ever)
    private bool _loading;
    private bool _failed;
    private string _queryKey = string.Empty;
    private CancellationTokenSource? _cts;

    private bool Visible => SearchText.Fold(Title).Length >= MinimumLength
                            && (_local.Count > 0 || _hits is not null || _loading || _failed);

    protected override void OnParametersSet()
    {
        var key = SearchText.Fold(Title) + "|" + SearchText.Fold(Artist);
        if (key == _queryKey)
            return;
        _queryKey = key;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _hits = null;
        _failed = false;
        _loading = false;

        if (SearchText.Fold(Title).Length < MinimumLength)
        {
            _local = [];
            return;
        }

        _local = Library
            .Where(i => SearchText.MatchesAllWords(i.Title, Title)
                        && (string.IsNullOrWhiteSpace(Artist) || SearchText.MatchesAllWords(i.Artist, Artist)))
            .OrderByDescending(i => i.IsFavorite)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .Take(LocalLimit)
            .ToList();

        _cts = new CancellationTokenSource();
        _loading = true;
        _ = SearchOnlineAsync(Title, Artist, _cts.Token);
    }

    private async Task SearchOnlineAsync(string title, string artist, CancellationToken ct)
    {
        try
        {
            await Task.Delay(Debounce, ct);
            var hits = await Search.SearchAsync(title, artist, OnlineLimit + LocalLimit, ct);
            if (ct.IsCancellationRequested)
                return;

            // A song already listed under "In My Songs" isn't repeated below it.
            var shownIds = _local.Select(i => i.Id).ToHashSet();
            _hits = hits?.Where(h => Existing(h) is not { } mine || !shownIds.Contains(mine.Id)).Take(OnlineLimit).ToList();
            _failed = hits is null;
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) { /* a newer keystroke took over */ }
        catch (Exception ex)
        {
            // best-effort: suggestions are a convenience; the form works without them
            Log.LogWarning(ex, "Song search failed");
            _loading = false;
            _failed = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    // The singer's own copy of an online hit, matched loosely (case, accents, punctuation), or null.
    private SongListItem? Existing(SongSearchHit hit)
    {
        var title = SearchText.Fold(hit.Title);
        var artist = SearchText.Fold(hit.Artist);
        return Library.FirstOrDefault(i => SearchText.Fold(i.Title) == title && SearchText.Fold(i.Artist) == artist);
    }

    private static string MineDetail(SongListItem item)
    {
        var sung = item.Performances.Count;
        var tail = sung == 0 ? "not sung yet" : sung == 1 ? "sung once" : $"sung {sung}×";
        return string.IsNullOrWhiteSpace(item.Artist) ? tail : $"{item.Artist} · {tail}";
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
