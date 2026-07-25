using System.Text.Json;
using KHost.Mobile.Clients.Json;
using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Enrichment;

/// <summary>
/// Parses an iTunes Search API response into a <see cref="TrackLookupResult"/>. Pure — no network. The API
/// returns <c>{ "resultCount": n, "results": [ { trackName, artistName, releaseDate, primaryGenreName, … } ] }</c>.
/// We do NOT trust the top-ranked result: iTunes free-text matching happily returns covers and unrelated
/// songs. Instead we keep only a result whose track name AND artist both match the requested song (after
/// light normalization) — better no data than wrong data.
/// </summary>
public static class ITunesResponseParser
{
    /// <summary>
    /// Returns metadata from the first result whose <c>trackName</c> and <c>artistName</c> both match
    /// <paramref name="requestedTitle"/> / <paramref name="requestedArtist"/> (case/punctuation/accent- and
    /// feature-suffix-insensitive).
    /// <para>Failing that, returns the closest near-miss as a <see cref="TrackLookupResult.Suggestion"/> so
    /// the caller can offer a spelling correction — see <see cref="FindSuggestion"/> for how narrow that is.
    /// Returns <see cref="TrackLookupResult.None"/> when nothing matches or the artist is unknown (nothing
    /// to verify against).</para>
    /// </summary>
    public static TrackLookupResult ParseBestMatch(string json, string requestedTitle, string requestedArtist)
    {
        if (string.IsNullOrWhiteSpace(json)
            || string.IsNullOrWhiteSpace(requestedTitle)
            || string.IsNullOrWhiteSpace(requestedArtist))
        {
            return TrackLookupResult.None;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return TrackLookupResult.None; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return TrackLookupResult.None;
            }

            var wantTitle = TrackTextNormalizer.Normalize(requestedTitle);
            var wantArtist = TrackTextNormalizer.Normalize(requestedArtist);

            foreach (var result in results.EnumerateArray())
            {
                var title = result.Str("trackName");
                var artist = result.Str("artistName");

                // Both must match — a right-title/wrong-artist cover (or vice versa) is rejected.
                if (TrackTextNormalizer.Normalize(title) != wantTitle || TrackTextNormalizer.Normalize(artist) != wantArtist)
                    continue;

                var genre = result.Str("primaryGenreName");
                var year = YearFromIsoDate(result.Str("releaseDate"));
                var artwork = UpscaleArtwork(result.Str("artworkUrl100"));
                return new TrackLookupResult(new TrackMetadata(title, artist, year, genre, artwork), null);
            }

            return new TrackLookupResult(null, FindSuggestion(results, wantTitle, wantArtist));
        }
    }

    // iTunes has no notion of an artist variant, so the anchor is plain normalized equality.
    private static TrackSuggestion? FindSuggestion(JsonElement results, string wantTitle, string wantArtist)
        => TrackSuggestionFinder.Best(
            results.EnumerateArray().Select(r => (r.Str("trackName"), r.Str("artistName"))),
            wantTitle,
            wantArtist,
            artist => TrackTextNormalizer.Normalize(artist) == wantArtist);

    // iTunes returns a 100×100 thumbnail URL like ".../source/100x100bb.jpg". Swap the dimensions for a
    // crisper 300×300 that still covers a card background without bloating the cached/base64-encoded image.
    private static string? UpscaleArtwork(string? url)
        => string.IsNullOrEmpty(url) ? url : url.Replace("100x100", "300x300", StringComparison.Ordinal);

    // releaseDate looks like "2004-06-08T07:00:00Z"; take the leading 4-digit year if present.
    private static int? YearFromIsoDate(string? isoDate)
        => isoDate is { Length: >= 4 } && int.TryParse(isoDate.AsSpan(0, 4), out var year)
            ? year
            : null;
}
