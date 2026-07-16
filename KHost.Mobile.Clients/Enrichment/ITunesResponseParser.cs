using System.Text.Json;
using KHost.Mobile.Clients.Json;
using KHost.Mobile.Clients.Matching;

namespace KHost.Mobile.Clients.Enrichment;

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
                return new TrackMetadata(title, artist, year, genre, artwork);
            }

            return null;
        }
    }

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
