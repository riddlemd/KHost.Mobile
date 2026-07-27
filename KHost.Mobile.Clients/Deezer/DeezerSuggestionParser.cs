using System.Text.Json;
using KHost.Mobile.Clients.Matching;
using KHost.Mobile.Json;

namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Parses a Deezer <c>/search</c> response into a spelling correction. Pure — no network. Same payload shape
/// as <see cref="DeezerCoverArtParser"/>, different question: not "which result is this song" but "does one of
/// these look like what the user meant to type".
/// </summary>
public static class DeezerSuggestionParser
{
    /// <summary>
    /// Returns the closest near-miss among the results, or null when nothing is close enough, the payload is
    /// unusable, or Deezer returned an error object.
    /// </summary>
    public static TrackSuggestion? ParseSuggestion(string json, string requestedTitle, string requestedArtist)
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
            // Deezer signals quota (code 4) and other faults as a 200 body with an "error" object.
            if (doc.RootElement.TryGetProperty("error", out _))
                return null;

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;

            return TrackSuggestionFinder.Best(
                data.EnumerateArray().Select(i => (i.Str("title"), i.Prop("artist").Str("name"))),
                TrackTextNormalizer.Normalize(requestedTitle),
                TrackTextNormalizer.Normalize(requestedArtist),
                // Reuse the cover-art path's tolerance for band-name variants ("The White Stripes" vs "White
                // Stripes"), so a variant anchors the match instead of being reported as a misspelling.
                artist => DeezerArtistMatcher.Matches(artist, requestedArtist));
        }
    }
}
