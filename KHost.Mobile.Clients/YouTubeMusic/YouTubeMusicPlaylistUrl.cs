using System.Text.RegularExpressions;

namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// Pure parsing of the shapes a user might paste for a YouTube Music playlist: a URL with a
/// <c>?list=…</c> query (music.youtube.com or youtube.com), a <c>browse/VL…</c> link, or a bare
/// playlist id. No network — trivially unit-testable.
/// </summary>
public static partial class YouTubeMusicPlaylistUrl
{
    // A YouTube playlist id following list= : letters, digits, - and _ (variable length: PL…=34, OLAK…=41).
    [GeneratedRegex(@"[?&]list=([A-Za-z0-9_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ListParamRegex();

    // A bare id (no URL). 13+ chars keeps us clear of an 11-char *video* id being mistaken for a playlist.
    [GeneratedRegex(@"^[A-Za-z0-9_-]{13,}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareIdRegex();

    /// <summary>
    /// Extracts the playlist id from a URL's <c>list=</c> query, a <c>browse/VL{id}</c> path, or a bare id.
    /// A leading <c>VL</c> (the browse-endpoint prefix) is stripped. Returns false when nothing matches.
    /// </summary>
    public static bool TryParseId(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        var listMatch = ListParamRegex().Match(trimmed);
        if (listMatch.Success)
        {
            id = Normalize(listMatch.Groups[1].Value);
            return true;
        }

        // browse/VL<id> links, or a bare id pasted on its own.
        var browseIndex = trimmed.IndexOf("browse/", StringComparison.Ordinal);
        if (browseIndex >= 0)
        {
            var candidate = trimmed[(browseIndex + "browse/".Length)..];
            if (BareIdRegex().IsMatch(candidate))
            {
                id = Normalize(candidate);
                return true;
            }
        }

        if (BareIdRegex().IsMatch(trimmed))
        {
            id = Normalize(trimmed);
            return true;
        }

        return false;
    }

    // The browse endpoint prefixes a playlist id with "VL"; the playlist page wants the bare id.
    private static string Normalize(string id) =>
        id.StartsWith("VL", StringComparison.Ordinal) && id.Length > 2 ? id[2..] : id;
}
