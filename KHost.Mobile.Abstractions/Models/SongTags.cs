namespace KHost.Mobile.Models;

/// <summary>
/// Normalization rules for a song's free-form <see cref="SongListItem.Tags"/>. Kept pure (no I/O) so it can be
/// unit-tested and shared by the tag input, the store write path, and import. There is deliberately no fixed tag
/// catalogue (unlike <see cref="Genres"/>) — tags are whatever the singer types; the in-use set is derived on the
/// fly for autocomplete and filtering.
/// </summary>
public static class SongTags
{
    /// <summary>Max characters kept for a single tag; longer input is trimmed to this length.</summary>
    public const int MaxLength = 30;

    /// <summary>Max tags kept per song; extras (after de-duplication) are dropped.</summary>
    public const int MaxCount = 12;

    /// <summary>
    /// Clean one candidate tag: trim surrounding whitespace, collapse internal runs of whitespace to a single
    /// space, drop a leading '#', and cap at <see cref="MaxLength"/>. Returns null for blank / all-punctuation
    /// input so callers can skip it.
    /// </summary>
    public static string? Clean(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var trimmed = tag.Trim().TrimStart('#').Trim();
        if (trimmed.Length == 0)
            return null;

        // Handles a pasted newline/tab too, not just spaces.
        var collapsed = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0)
            return null;

        return collapsed.Length > MaxLength ? collapsed[..MaxLength].Trim() : collapsed;
    }

    /// <summary>
    /// Normalize a set of tags for persistence: clean each (see <see cref="Clean"/>), drop blanks, de-duplicate
    /// case-insensitively keeping the first-seen casing, and cap the count at <see cref="MaxCount"/> (in order).
    /// Order is otherwise preserved. Always returns a fresh list.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? tags)
    {
        var result = new List<string>();
        if (tags is null)
            return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in tags)
        {
            if (Clean(raw) is not { } tag)
                continue;
            if (!seen.Add(tag))
                continue;   // case-insensitive dupe — keep the first casing we saw
            result.Add(tag);
            if (result.Count >= MaxCount)
                break;
        }

        return result;
    }
}
