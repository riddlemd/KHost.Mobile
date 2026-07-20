using System.Text.Json;
using System.Text.RegularExpressions;
using KHost.Mobile.Clients.Json;

namespace KHost.Mobile.Clients.Spotify;

/// <summary>
/// Extracts the tracklist from a Spotify <c>/embed/playlist/{id}</c> page's
/// <c>__NEXT_DATA__</c> JSON blob. This is an UNOFFICIAL, undocumented shape (the embed
/// player's Next.js props) and can change without notice, so parsing is deliberately
/// tolerant: it searches for the data by property name rather than a fixed nesting path,
/// and skips anything it doesn't recognize instead of throwing. Pure — no network.
/// </summary>
public static partial class SpotifyEmbedParser
{
    [GeneratedRegex(
        "<script id=\"__NEXT_DATA__\" type=\"application/json\">(.*?)</script>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextDataRegex();

    private const string TrackUriPrefix = "spotify:track:";

    /// <summary>
    /// Parses embed HTML into a playlist import. Throws <see cref="SpotifyImportException"/>
    /// when the data blob is missing or holds no tracklist (page shape changed, or the id
    /// wasn't a playlist).
    /// </summary>
    public static SpotifyPlaylistImport Parse(string html)
    {
        var match = NextDataRegex().Match(html ?? string.Empty);
        if (!match.Success)
            throw new SpotifyImportException(
                "Couldn't read the playlist — Spotify may have changed its embed format.");

        using var doc = JsonDocument.Parse(match.Groups[1].Value);

        if (!TryFindArray(doc.RootElement, "trackList", out var trackList))
            throw new SpotifyImportException("No tracks were found for that playlist.");

        var rawCount = trackList.GetArrayLength();
        var tracks = new List<SpotifyTrack>(rawCount);
        foreach (var entry in trackList.EnumerateArray())
        {
            var title = entry.Str("title");
            if (string.IsNullOrWhiteSpace(title))
                continue;   // skip unnamed / non-track rows defensively

            var artist = entry.Str("subtitle") ?? string.Empty;
            var trackId = TrackIdFromUri(entry.Str("uri"));
            tracks.Add(new SpotifyTrack(title.Trim(), artist.Trim(), trackId));
        }

        var name = TryFindPlaylistName(doc.RootElement);

        // The embed caps at ~100 items; if we got that many, assume the playlist may be longer.
        return new SpotifyPlaylistImport(name, tracks, LikelyTruncated: rawCount >= 100);
    }

    // Depth-first search for the first array-valued property with the given name. Robust to
    // the exact nesting (props.pageProps.state.data.entity…) shifting between Spotify deploys.
    private static bool TryFindArray(JsonElement element, string propertyName, out JsonElement found)
    {
        found = default;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName) && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        found = property.Value;
                        return true;
                    }

                    if (TryFindArray(property.Value, propertyName, out found))
                        return true;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (TryFindArray(item, propertyName, out found))
                        return true;
                break;
        }

        return false;
    }

    // Best-effort — returns null if the page doesn't expose it.
    private static string? TryFindPlaylistName(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String && type.ValueEquals("playlist")
                    && element.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String)
                {
                    return name.GetString();
                }

                foreach (var property in element.EnumerateObject())
                {
                    var result = TryFindPlaylistName(property.Value);
                    if (result is not null)
                        return result;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var result = TryFindPlaylistName(item);
                    if (result is not null)
                        return result;
                }
                break;
        }

        return null;
    }

    private static string? TrackIdFromUri(string? uri)
        => uri is not null && uri.StartsWith(TrackUriPrefix, StringComparison.Ordinal)
            ? uri[TrackUriPrefix.Length..]
            : null;
}
