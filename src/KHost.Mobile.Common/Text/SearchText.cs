using System.Globalization;
using System.Text;

namespace KHost.Mobile.Common.Text;

/// <summary>
/// Forgiving text comparison for as-you-type matching, where what's typed is missing the punctuation, accents and
/// capitals of what's stored ("dont stop" should find "Don't Stop"). Domain-free: it knows letters, not songs.
/// </summary>
public static class SearchText
{
    /// <summary>
    /// Lowercase, accents dropped, apostrophes removed (so "don't" and "dont" agree), every other non-alphanumeric
    /// run folded to one space, and trimmed. Null or blank gives an empty string.
    /// </summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch is '\'' or '’' or '‘')
                continue;
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && sb.Length > 0)
                    sb.Append(' ');
                pendingSpace = false;
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                pendingSpace = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Whether every word of <paramref name="query"/> appears in <paramref name="text"/>, in any order, as the
    /// start of a word ("bri" finds "Mr. Brightside"; "side" does not). Both sides are <see cref="Fold"/>ed. An
    /// empty query matches nothing.
    /// </summary>
    public static bool MatchesAllWords(string? text, string? query)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return false;

        var haystack = " " + Fold(text);
        return words.All(w => haystack.Contains(" " + w, StringComparison.Ordinal));
    }
}
