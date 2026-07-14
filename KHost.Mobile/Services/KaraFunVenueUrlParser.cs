using System.Text.RegularExpressions;

namespace KHost.Mobile.Services;

/// <summary>
/// Parses a KaraFun venue ID out of a pasted venue URL (or a bare ID). Pure — no network. Accepts the shapes a
/// user is likely to paste, rejects everything else.
/// </summary>
public static partial class KaraFunVenueUrlParser
{
    // A KaraFun venue link carries the venue as the first path segment: karafun.com/076217/… — a run of digits.
    // The whole run is captured (leading zeros are part of the ID and must be kept — e.g. 076217).
    [GeneratedRegex(@"karafun\.com/(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrlVenueRegex();

    // A bare ID typed straight in — digits only.
    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex BareIdRegex();

    /// <summary>
    /// True if <paramref name="input"/> is a KaraFun venue URL or a bare numeric venue ID; <paramref name="id"/>
    /// then holds the venue ID exactly as it should appear in the link (leading zeros preserved). False (and an
    /// empty id) for anything else.
    /// </summary>
    public static bool TryParseId(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        var urlMatch = UrlVenueRegex().Match(trimmed);
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value;
            return true;
        }

        if (BareIdRegex().IsMatch(trimmed))
        {
            id = trimmed;
            return true;
        }

        return false;
    }
}
