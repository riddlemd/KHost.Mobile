using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Parses a Deezer <c>/search</c> response into a cover-art URL. Pure — no network. The API returns
/// <c>{ "data": [ { "title", "artist": { "name" }, "album": { "cover_big", "cover_xl", … } } ] }</c>.
/// We return the album cover of the first result whose track title matches AND whose artist matches (after
/// normalization), or null when nothing matches — better no art than the wrong cover.
/// </summary>
public static class DeezerCoverArtParser
{
    /// <summary>
    /// Returns the album cover URL from the first result whose <c>title</c> and <c>artist.name</c> match
    /// <paramref name="requestedTitle"/> / <paramref name="requestedArtist"/>, or null when nothing matches,
    /// the payload is unusable, or Deezer returned an error object (quota/etc. — treated as "no cover").
    /// </summary>
    public static string? ParseCoverArtUrl(string json, string requestedTitle, string requestedArtist)
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
            // Deezer signals quota (code 4) and other faults as a 200 body with an "error" object, not an
            // HTTP status. As a best-effort fallback we just treat any Deezer-side error as "no cover".
            if (doc.RootElement.TryGetProperty("error", out _))
                return null;

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;

            var wantTitle = Normalize(requestedTitle);
            var wantArtist = Tokens(requestedArtist);

            foreach (var item in data.EnumerateArray())
            {
                if (Normalize(Str(item, "title")) != wantTitle)
                    continue;
                if (!ArtistMatches(Str(Child(item, "artist"), "name"), wantArtist))
                    continue;
                if (CoverUrl(Child(item, "album")) is string cover)
                    return cover;
            }

            return null;
        }
    }

    // Prefer a 500×500 cover (good on a card without bloating the cached/base64-encoded image), then fall
    // back through the other Deezer sizes. Returns null when the album carries no usable cover field.
    private static string? CoverUrl(JsonElement album)
    {
        foreach (var field in (ReadOnlySpan<string>)["cover_big", "cover_xl", "cover_medium", "cover_small", "cover"])
            if (Str(album, field) is { Length: > 0 } url)
                return url;
        return null;
    }

    // Deezer's artist string can be a superset/subset of ours ("White Stripes" vs "The White Stripes",
    // "Hall & Oates" vs "Daryl Hall & John Oates", "Ben Folds" vs "Ben Folds Five"). Accept an exact token
    // match, or a subset either way as long as the shorter side has ≥2 meaningful tokens — that keeps the
    // real variants while rejecting a single-token coincidence ("Prince" ⊄ "Prince Royce") or a wrong artist
    // that only shares a first name ("Bo Burnham" vs "Bo Hazard").
    private static bool ArtistMatches(string? resultArtist, HashSet<string> wantArtist)
    {
        var got = Tokens(resultArtist);
        if (got.Count == 0 || wantArtist.Count == 0)
            return false;
        if (got.SetEquals(wantArtist))
            return true;
        var (smaller, larger) = got.Count <= wantArtist.Count ? (got, wantArtist) : (wantArtist, got);
        return smaller.Count >= 2 && smaller.IsSubsetOf(larger);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal) { "the", "and", "a" };

    private static HashSet<string> Tokens(string? value)
        => Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t))
            .ToHashSet(StringComparer.Ordinal);

    // Same light normalization the iTunes parser uses: lowercase, drop bracketed/feature/version qualifiers,
    // strip accents, fold every non-alphanumeric char to a space, and collapse whitespace.
    private static readonly string[] Qualifiers = [" feat.", " feat ", " featuring ", " ft. ", " ft ", " - "];

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToLowerInvariant();
        text = StripBetween(text, '(', ')');
        text = StripBetween(text, '[', ']');

        foreach (var marker in Qualifiers)
        {
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
                text = text[..idx];
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

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

    private static JsonElement Child(JsonElement obj, string propertyName)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var value)
            ? value
            : default;

    private static string? Str(JsonElement obj, string propertyName)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
