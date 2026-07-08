using System.Text.Json;

namespace KHost.Mobile.Client.Enrichment;

/// <summary>
/// Parses an iTunes Search API response into <see cref="TrackMetadata"/>. Pure — no network. The API
/// returns <c>{ "resultCount": n, "results": [ { trackName, artistName, releaseDate, primaryGenreName, … } ] }</c>;
/// we take the first result and pull the year (from the ISO <c>releaseDate</c>) and the primary genre.
/// </summary>
public static class ITunesResponseParser
{
    /// <summary>Parses the first result, or returns null when there are none / the payload is unusable.</summary>
    public static TrackMetadata? ParseFirst(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var title = Str(first, "trackName");
            var artist = Str(first, "artistName");
            var genre = Str(first, "primaryGenreName");
            var year = YearFromIsoDate(Str(first, "releaseDate"));

            return new TrackMetadata(title, artist, year, genre);
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
}
