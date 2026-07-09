using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KHost.Mobile.Client.Enrichment;

/// <summary>
/// Parses an iTunes Search API response into <see cref="TrackMetadata"/>. Pure — no network. The API
/// returns <c>{ "resultCount": n, "results": [ { trackName, artistName, releaseDate, primaryGenreName, … } ] }</c>.
/// We do NOT trust the top-ranked result: iTunes free-text matching happily returns covers and unrelated
/// songs. Instead we return the first result whose track name AND artist both match the requested song
/// (after light normalization), or null when none match — better no data than wrong data.
/// </summary>
public static class ITunesResponseParser
{
    /// <summary>
    /// Returns metadata from the first result whose <c>trackName</c> and <c>artistName</c> both match
    /// <paramref name="requestedTitle"/> / <paramref name="requestedArtist"/> (case/punctuation/accent- and
    /// feature-suffix-insensitive), or null when nothing matches or the artist is unknown (nothing to verify against).
    /// </summary>
    public static TrackMetadata? ParseBestMatch(string json, string requestedTitle, string requestedArtist)
    {
        if (string.IsNullOrWhiteSpace(json)
            || string.IsNullOrWhiteSpace(requestedTitle)
            || string.IsNullOrWhiteSpace(requestedArtist))
        {
            return null;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var wantTitle = Normalize(requestedTitle);
            var wantArtist = Normalize(requestedArtist);

            foreach (var result in results.EnumerateArray())
            {
                var title = Str(result, "trackName");
                var artist = Str(result, "artistName");

                // Both must match — a right-title/wrong-artist cover (or vice versa) is rejected.
                if (Normalize(title) != wantTitle || Normalize(artist) != wantArtist)
                    continue;

                var genre = Str(result, "primaryGenreName");
                var year = YearFromIsoDate(Str(result, "releaseDate"));
                return new TrackMetadata(title, artist, year, genre);
            }

            return null;
        }
    }

    // releaseDate looks like "2004-06-08T07:00:00Z"; take the leading 4-digit year if present.
    private static int? YearFromIsoDate(string? isoDate)
        => isoDate is { Length: >= 4 } && int.TryParse(isoDate.AsSpan(0, 4), out var year)
            ? year
            : null;

    private static string? Str(JsonElement obj, string propertyName)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // Light normalization for match comparison: lowercase, drop bracketed/feature/version qualifiers,
    // strip accents, and reduce to alphanumeric words. Deliberately conservative — it should accept the
    // same song written slightly differently ("Wow, I Can Get Sexual Too" vs "Wow I Can Get Sexual Too"),
    // never bridge two different songs. Over-trimming just fails the match, which errs toward "don't populate".
    private static readonly string[] Qualifiers = [" feat.", " feat ", " featuring ", " ft. ", " ft ", " - "];

    private static string Normalize(string? value)
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
