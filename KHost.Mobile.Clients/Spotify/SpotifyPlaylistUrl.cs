using System.Text.RegularExpressions;

namespace KHost.Mobile.Clients.Spotify;

/// <summary>
/// Pure parsing of the shapes a user might paste for a Spotify playlist: a web URL
/// (<c>https://open.spotify.com/playlist/{id}?si=…</c>), a URI (<c>spotify:playlist:{id}</c>),
/// or a bare 22-char id. No network — trivially unit-testable.
/// </summary>
public static partial class SpotifyPlaylistUrl
{
    // Spotify ids are 22-char base62. Match the id that follows "playlist/" (URL) or
    // "playlist:" (URI); the fixed length stops us before any "?si=…" query tail.
    [GeneratedRegex(@"playlist[:/]([A-Za-z0-9]{22})", RegexOptions.CultureInvariant)]
    private static partial Regex PlaylistIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{22}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareIdRegex();

    /// <summary>
    /// Extracts the 22-char playlist id from a URL, a <c>spotify:</c> URI, or a bare id.
    /// Returns false (and an empty id) when the input contains no recognizable playlist id.
    /// </summary>
    public static bool TryParseId(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        if (BareIdRegex().IsMatch(trimmed))
        {
            id = trimmed;
            return true;
        }

        var match = PlaylistIdRegex().Match(trimmed);
        if (match.Success)
        {
            id = match.Groups[1].Value;
            return true;
        }

        return false;
    }
}
