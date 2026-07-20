using System.Text.RegularExpressions;

namespace KHost.Mobile.Services;

/// <summary>
/// Parses a KaraFun venue ID out of a pasted venue URL (or a bare ID). Pure — no network. Accepts the shapes a
/// user is likely to paste, rejects everything else.
/// </summary>
public static partial class KaraFunVenueUrlParser
{
    // A KaraFun venue link carries the venue as the first path segment: karafun.com/012345/… — a run of digits.
    // The whole run is captured (leading zeros are part of the ID and must be kept — e.g. 012345).
    [GeneratedRegex(@"karafun\.com/(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrlVenueRegex();

    // A bare ID typed straight in — digits only.
    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex BareIdRegex();

    // The exact hosts a KaraFun venue link may use. Matched against Uri.Host (not a substring) so look-alikes like
    // evilkarafun.com or karafun.com.evil.com are rejected.
    private static readonly string[] AllowedHosts = ["karafun.com", "www.karafun.com"];

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

    /// <summary>
    /// Strict variant for untrusted input such as a scanned QR payload: succeeds only when <paramref name="input"/>
    /// is an absolute http/https URL whose host is exactly a KaraFun host, with a numeric venue as its first path
    /// segment; <paramref name="id"/> then holds that venue ID (leading zeros preserved). Unlike
    /// <see cref="TryParseId"/> it rejects look-alike hosts (e.g. evilkarafun.com) and bare IDs — a QR code should
    /// carry a full venue link, and we don't want an arbitrary scanned string to become a venue.
    /// </summary>
    public static bool TryParseVenueUrl(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            return false;

        var firstSegment = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstSegment is null || !BareIdRegex().IsMatch(firstSegment))
            return false;

        id = firstSegment;
        return true;
    }
}
