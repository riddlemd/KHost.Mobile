using System.Text.Json;
using System.Text.RegularExpressions;
using KHost.Mobile.Abstractions.Clients.Search;
using KHost.Mobile.Clients.Matching;
using KHost.Mobile.Common.Json;

namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// Turns a Deezer <c>/search</c> reply into the short, tidy list the Add-a-song panel shows. Pure — no network.
/// Deezer returns every release of a hit song separately (original, "(2024 Remaster)", eight live BBC sessions)
/// plus tribute covers, so the raw top ten is often one song ten times. Three rules make it a list of songs:
/// <list type="number">
/// <item>Drop covers and karaoke/tribute releases ("(Cover of Journey)", "80's Greatest Hits").</item>
/// <item>Fold versions of one song together, keyed on the normalised title + artist; the count rides along.</item>
/// <item>Show the plainest version — one whose title had no aside — when the fold has one.</item>
/// </list>
/// </summary>
internal static partial class DeezerSongSearchParser
{
    // A title aside that marks a cover, not the artist's own recording.
    [GeneratedRegex(@"\b(cover of|karaoke|made famous by|originally performed|in the style of|as made famous|tribute to)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CoverTitle();

    // An artist name that is a karaoke label or an anonymous compilation act rather than a performer.
    [GeneratedRegex(@"\b(karaoke|tribute|greatest hits|hits of the|backing track|sing-?along|cover band|in the style)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CoverArtist();

    // "(feat. Amy Winehouse)" / "[ft. X]" / "(with X)" in a title — moved onto the artist line.
    [GeneratedRegex(@"[\(\[](?:feat\.?|ft\.?|featuring|with)\s+([^\)\]]+)[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex Featuring();

    // " - Remastered 2011", " - Live", " - Radio Edit": a dash-suffix that names a version rather than the song.
    [GeneratedRegex(@"\s+-\s+.*\b(remaster(ed)?|live|version|edit|mix|mono|stereo|demo|acoustic|re-?recorded|single)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSuffix();

    public static IReadOnlyList<SongSearchHit>? Parse(string json, int limit)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            // Deezer reports quota and other faults as a 200 with an "error" object: that's "couldn't search".
            if (doc.RootElement.TryGetProperty("error", out _))
                return null;
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;

            var groups = new List<Group>();
            var byKey = new Dictionary<string, Group>();
            foreach (var item in data.EnumerateArray())
            {
                var rawTitle = item.Str("title");
                var artist = item.Prop("artist").Str("name");
                if (string.IsNullOrWhiteSpace(rawTitle) || string.IsNullOrWhiteSpace(artist))
                    continue;
                if (CoverTitle().IsMatch(rawTitle) || CoverArtist().IsMatch(artist))
                    continue;

                var key = TrackTextNormalizer.Normalize(rawTitle) + "|" + TrackTextNormalizer.Normalize(artist);
                if (key.StartsWith('|'))
                    continue;   // a title that was nothing but an aside

                var feat = Featuring().Match(rawTitle);
                var shownArtist = feat.Success ? $"{artist.Trim()} ft. {feat.Groups[1].Value.Trim()}" : artist.Trim();
                var shownTitle = VersionSuffix().Replace(TrackTextNormalizer.StripAsides(rawTitle), "").Trim();
                var plain = shownTitle == rawTitle.Trim();
                var album = item.Prop("album");
                var hit = new SongSearchHit(shownTitle, shownArtist, album.Str("title"),
                    CoverUrl(album, "cover_big", "cover_xl", "cover_medium"), CoverUrl(album, "cover_medium", "cover_small", "cover_big"), 0);

                if (byKey.TryGetValue(key, out var group))
                {
                    group.Count++;
                    if (plain && !group.Plain)
                    {
                        group.Hit = hit;   // the plainest version represents the song; it keeps its rank slot
                        group.Plain = true;
                    }
                    continue;
                }

                group = new Group { Hit = hit, Plain = plain, Count = 1 };
                byKey[key] = group;
                groups.Add(group);
            }

            return groups
                .Take(Math.Max(0, limit))
                .Select(g => g.Hit with { OtherVersions = g.Count - 1 })
                .ToList();
        }
    }

    // The kept cover is cover_big (500 px), what the cover-art lookup stores, so a picked song's card looks the same
    // as an enriched one. The row thumbnail is cover_medium (250 px): sharp at 40 px on a 3× screen, a tenth the bytes.
    private static string? CoverUrl(JsonElement album, params string[] preference)
    {
        foreach (var field in preference.Append("cover"))
            if (album.Str(field) is { Length: > 0 } url)
                return url;
        return null;
    }

    private sealed class Group
    {
        public required SongSearchHit Hit { get; set; }
        public bool Plain { get; set; }
        public int Count { get; set; }
    }
}
