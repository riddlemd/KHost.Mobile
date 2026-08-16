namespace KHost.Mobile.UI.Components;

public sealed partial class TagInput
{
    /// <summary>The song's current tags (already normalized by the parent). Rendered as removable chips.</summary>
    [Parameter] public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>Raised with a fresh, de-duplicated list whenever a tag is added or removed.</summary>
    [Parameter] public EventCallback<List<string>> TagsChanged { get; set; }

    /// <summary>Existing tags across the whole list, offered as reuse-suggestions (filtered by what's typed).</summary>
    [Parameter] public IReadOnlyList<string>? Suggestions { get; set; }

    /// <summary>Placeholder shown in the empty entry when there are no chips yet.</summary>
    [Parameter] public string Placeholder { get; set; } = "Add a tag…";

    private ElementReference _entryRef;
    private string _entry = string.Empty;

    private IEnumerable<string> VisibleSuggestions
    {
        get
        {
            var typed = _entry.Trim();
            return (Suggestions ?? [])
                .Where(s => !Tags.Any(t => string.Equals(t, s, StringComparison.OrdinalIgnoreCase)))
                .Where(s => typed.Length == 0 || s.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Take(8);
        }
    }

    private Task FocusEntryAsync() => _entryRef.FocusAsync().AsTask();

    // A pasted or typed comma commits every complete token, leaving the trailing fragment in the entry.
    private async Task OnEntryChangedAsync()
    {
        if (!_entry.Contains(','))
            return;

        var parts = _entry.Split(',');
        for (var i = 0; i < parts.Length - 1; i++)
            await CommitAsync(parts[i]);
        _entry = parts[^1];
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await CommitAsync(_entry);
        else if (e.Key == "Backspace" && _entry.Length == 0 && Tags.Count > 0)
            await RemoveAsync(Tags[^1]);
    }

    private async Task CommitAsync(string candidate)
    {
        _entry = string.Empty;

        if (SongTags.Clean(candidate) is not { } tag)
            return;
        if (Tags.Count >= SongTags.MaxCount)
            return;
        if (Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            return;

        await TagsChanged.InvokeAsync([.. Tags, tag]);
    }

    private async Task RemoveAsync(string tag)
    {
        var next = Tags.Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToList();
        await TagsChanged.InvokeAsync(next);
    }
}
