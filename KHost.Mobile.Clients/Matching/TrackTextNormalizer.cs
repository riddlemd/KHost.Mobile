using System.Globalization;
using System.Text;

namespace KHost.Mobile.Clients.Matching;

/// <summary>
/// Shared, pure text normalization for matching a requested song's title/artist against a third-party
/// search result. Used by the metadata/cover-art parsers (iTunes, Deezer) which cannot trust a free-text
/// API's top-ranked hit and must instead verify that a candidate's title and artist match what was asked for.
/// </summary>
/// <remarks>
/// Deliberately conservative: it should accept the same song written slightly differently
/// ("Wow, I Can Get Sexual Too" vs "Wow I Can Get Sexual Too"), never bridge two different songs.
/// Over-trimming just fails the match, which errs toward "don't populate" — better no data than wrong data.
/// </remarks>
internal static class TrackTextNormalizer
{
    // Trailing qualifiers cut before comparison: "song - live", "artist feat. x", "x featuring y".
    private static readonly string[] Qualifiers = [" feat.", " feat ", " featuring ", " ft. ", " ft ", " - "];

    /// <summary>
    /// Normalizes a title/artist for match comparison: lowercase, drop bracketed/feature/version qualifiers,
    /// strip accents, fold every non-alphanumeric char to a space, and collapse whitespace. Returns empty
    /// for null/blank input.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToLowerInvariant();

        // Drop parenthetical/bracketed asides: "(feat. X)", "(Remastered 2011)", "[Live]".
        text = StripBetween(text, '(', ')');
        text = StripBetween(text, '[', ']');

        // Cut trailing qualifiers: "song - live", "artist feat. x", "x featuring y".
        foreach (var marker in Qualifiers)
        {
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
                text = text[..idx];
        }

        // Strip accents, and fold every non-alphanumeric char to a space.
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        // Collapse whitespace to single spaces.
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string StripBetween(string text, char open, char close)
    {
        while (true)
        {
            var start = text.IndexOf(open);
            if (start < 0)
                return text;
            var end = text.IndexOf(close, start + 1);
            if (end < 0)
                return text;
            text = text.Remove(start, end - start + 1);
        }
    }
}
