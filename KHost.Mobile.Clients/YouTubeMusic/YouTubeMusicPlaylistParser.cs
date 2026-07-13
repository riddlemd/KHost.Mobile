using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// Extracts the tracklist from a YouTube Music <c>/playlist?list={id}</c> page. YT Music embeds its
/// data inside <c>initialData.push({ ... data: '&lt;JS-escaped JSON&gt;' })</c> — a single-quoted string
/// with <c>\xNN</c> escapes — so this un-escapes and parses each such blob, then reads the rows at
/// <c>musicResponsiveListItemRenderer.flexColumns[]</c> (column 0 = title, column 1 = artist).
///
/// This is an UNOFFICIAL, undocumented shape and can change without notice, so parsing is deliberately
/// tolerant: it searches by property name rather than a fixed path and skips anything unrecognized. Pure — no network.
/// </summary>
public static partial class YouTubeMusicPlaylistParser
{
    [GeneratedRegex(@"data:\s*'", RegexOptions.CultureInvariant)]
    private static partial Regex DataBlobRegex();

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex VideoIdRegex();

    /// <summary>
    /// Parses YT Music playlist HTML into an import. Throws <see cref="YouTubeMusicImportException"/> when
    /// no data blob can be read or the playlist has no tracks (page shape changed, or it isn't public).
    /// </summary>
    public static YouTubeMusicPlaylistImport Parse(string html)
    {
        html ??= string.Empty;

        // A page carries several data: '…' blobs; pick whichever parses to the most track rows.
        List<YouTubeMusicTrack>? best = null;
        var bestTruncated = false;
        var sawAnyJson = false;

        foreach (var blob in EnumerateDataBlobs(html))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(blob); }
            catch (JsonException) { continue; }

            using (doc)
            {
                sawAnyJson = true;
                var tracks = new List<YouTubeMusicTrack>();
                CollectTracks(doc.RootElement, tracks);
                if (best is null || tracks.Count > best.Count)
                {
                    best = tracks;
                    bestTruncated = ContainsProperty(doc.RootElement, "continuationItemRenderer");
                }
            }
        }

        if (!sawAnyJson)
            throw new YouTubeMusicImportException("Couldn't read the playlist — YouTube Music may have changed its page format.");

        if (best is null || best.Count == 0)
            throw new YouTubeMusicImportException("No tracks were found for that playlist. Make sure it's public.");

        return new YouTubeMusicPlaylistImport(PlaylistName(html), best, bestTruncated);
    }

    // Yields the decoded JSON of every `data: '…'` assignment (the single-quoted, \xNN-escaped payloads).
    private static IEnumerable<string> EnumerateDataBlobs(string html)
    {
        foreach (Match m in DataBlobRegex().Matches(html))
        {
            var start = m.Index + m.Length;   // first char after the opening quote
            var i = start;
            for (; i < html.Length; i++)
            {
                if (html[i] == '\\') { i++; continue; }   // skip the escaped char
                if (html[i] == '\'') break;               // unescaped closing quote
            }
            if (i <= html.Length)
                yield return JsUnescape(html.AsSpan(start, i - start));
        }
    }

    // Decodes a single-quoted JS string body: \xHH, \uHHHH, and the standard one-char escapes.
    private static string JsUnescape(ReadOnlySpan<char> s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }

            var n = s[i + 1];
            switch (n)
            {
                case 'x' when i + 3 < s.Length:
                    sb.Append((char)Convert.ToInt32(new string(s.Slice(i + 2, 2)), 16));
                    i += 3;
                    break;
                case 'u' when i + 5 < s.Length:
                    sb.Append((char)Convert.ToInt32(new string(s.Slice(i + 2, 4)), 16));
                    i += 5;
                    break;
                case 'n': sb.Append('\n'); i++; break;
                case 't': sb.Append('\t'); i++; break;
                case 'r': sb.Append('\r'); i++; break;
                case 'b': sb.Append('\b'); i++; break;
                case 'f': sb.Append('\f'); i++; break;
                default: sb.Append(n); i++; break;   // \\ \/ \' \" and anything else -> literal next char
            }
        }
        return sb.ToString();
    }

    // Depth-first: each musicResponsiveListItemRenderer with a title + video id becomes a track.
    private static void CollectTracks(JsonElement element, List<YouTubeMusicTrack> acc)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("musicResponsiveListItemRenderer", out var row) && row.ValueKind == JsonValueKind.Object)
                {
                    var track = FromRow(row);
                    if (track is not null)
                        acc.Add(track);
                }
                foreach (var property in element.EnumerateObject())
                    CollectTracks(property.Value, acc);
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectTracks(item, acc);
                break;
        }
    }

    private static YouTubeMusicTrack? FromRow(JsonElement row)
    {
        var columns = row.Prop("flexColumns");
        if (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() == 0)
            return null;

        var title = FlexColumnText(columns, 0);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var videoId = VideoId(row);
        if (videoId is null)
            return null;

        var artist = FlexColumnText(columns, 1) ?? string.Empty;
        return new YouTubeMusicTrack(title.Trim(), artist.Trim(), videoId);
    }

    // Concatenates the text runs of flexColumns[index].
    private static string? FlexColumnText(JsonElement columns, int index)
    {
        if (index >= columns.GetArrayLength())
            return null;

        var runs = columns[index].Prop("musicResponsiveListItemFlexColumnRenderer").Prop("text").Prop("runs");
        if (runs.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();
        foreach (var run in runs.EnumerateArray())
        {
            var text = run.Str("text");
            if (text is not null)
                sb.Append(text);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string? VideoId(JsonElement row)
    {
        // Preferred: the playlist item's own id.
        var direct = row.Prop("playlistItemData").Str("videoId");
        if (direct is not null && VideoIdRegex().IsMatch(direct))
            return direct;

        // Fallback: the play-button's watch endpoint. Found by property name to survive layout shuffles.
        return FindVideoId(row);
    }

    private static string? FindVideoId(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("watchEndpoint"))
                    {
                        var id = property.Value.Str("videoId");
                        if (id is not null && VideoIdRegex().IsMatch(id))
                            return id;
                    }
                    var nested = FindVideoId(property.Value);
                    if (nested is not null)
                        return nested;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindVideoId(item);
                    if (nested is not null)
                        return nested;
                }
                break;
        }
        return null;
    }

    private static bool ContainsProperty(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(name))
                        return true;
                    if (ContainsProperty(property.Value, name))
                        return true;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (ContainsProperty(item, name))
                        return true;
                break;
        }
        return false;
    }

    private static string? PlaylistName(string html)
    {
        var match = TitleRegex().Match(html);
        if (!match.Success)
            return null;

        var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        foreach (var suffix in (ReadOnlySpan<string>)["- YouTube Music", "- YouTube"])
        {
            if (title.EndsWith(suffix, StringComparison.Ordinal))
            {
                title = title[..^suffix.Length].Trim();
                break;
            }
        }
        return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    // --- JsonElement navigation helpers (null-safe: a missing/wrong-kind hop yields Undefined) ----

    private static JsonElement Prop(this JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : default;

    private static string? Str(this JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
