using System.Text.Json;
using KHost.Mobile.Clients.Matching;
using KHost.Mobile.Json;

namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Parses a Deezer <c>/search</c> response into a cover-art URL. Pure — no network. The API returns
/// <c>{ "data": [ { "title", "artist": { "name" }, "album": { "cover_big", "cover_xl", … } } ] }</c>.
/// Title AND artist must both match: better no art than the wrong cover.
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
            // HTTP status. Any Deezer-side error is treated as "no cover".
            if (doc.RootElement.TryGetProperty("error", out _))
                return null;

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;

            var wantTitle = TrackTextNormalizer.Normalize(requestedTitle);
            var wantArtist = DeezerArtistMatcher.Tokens(requestedArtist);

            foreach (var item in data.EnumerateArray())
            {
                if (TrackTextNormalizer.Normalize(item.Str("title")) != wantTitle)
                    continue;
                if (!DeezerArtistMatcher.Matches(item.Prop("artist").Str("name"), wantArtist))
                    continue;
                if (CoverUrl(item.Prop("album")) is string cover)
                    return cover;
            }

            return null;
        }
    }

    // cover_big is Deezer's 500×500 — sharp enough without bloating the cached/base64-encoded image.
    private static string? CoverUrl(JsonElement album)
    {
        foreach (var field in (ReadOnlySpan<string>)["cover_big", "cover_xl", "cover_medium", "cover_small", "cover"])
            if (album.Str(field) is { Length: > 0 } url)
                return url;
        return null;
    }

}
