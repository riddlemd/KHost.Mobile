using System.Text.Json;
using KHost.Mobile.Clients.Json;

namespace KHost.Mobile.Clients.Lyrics;

/// <summary>
/// Parses an LRCLIB <c>/api/search</c> response (a JSON array of records) into a <see cref="LyricsResult"/>.
/// Pure — no network. Each record looks like
/// <c>{ id, trackName, artistName, albumName, duration, instrumental, plainLyrics, syncedLyrics }</c>.
/// Mirrors KHost's "take the first result" behaviour, but prefers the first record that actually carries
/// lyrics (or is flagged instrumental) so a bare metadata-only hit doesn't win. Returns null when the
/// array is empty or unparseable.
/// </summary>
public static class LrcLibResponseParser
{
    public static LyricsResult? ParseFirst(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            JsonElement? firstOverall = null;
            foreach (var record in doc.RootElement.EnumerateArray())
            {
                firstOverall ??= record;
                if (record.Bool("instrumental") || !string.IsNullOrWhiteSpace(record.Str("plainLyrics")))
                    return Map(record);
            }

            // No record carried lyrics — fall back to the first (surfaces as "no lyrics" in the UI).
            return firstOverall is { } f ? Map(f) : null;
        }
    }

    private static LyricsResult Map(JsonElement r) => new(
        MatchedTitle: r.Str("trackName"),
        MatchedArtist: r.Str("artistName"),
        PlainLyrics: r.Str("plainLyrics"),
        SyncedLyrics: r.Str("syncedLyrics"),
        Instrumental: r.Bool("instrumental"));
}
